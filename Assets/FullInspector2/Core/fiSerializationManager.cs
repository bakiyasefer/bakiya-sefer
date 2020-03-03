using System;
using System.Collections.Generic;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace FullInspector.Internal {
    /// <summary>
    /// This class manages the deserialization and serialization of objects when we are in the editor. It will automatically register
    /// itself to be updated inside of the editor. If we're in a player, then calls to this class are no-ops since either serialization
    /// callbacks or Awake() will be used to serialize objects.
    /// </summary>
    public static class fiSerializationManager {
        static fiSerializationManager() {
            if (fiUtility.IsEditor) {
                fiLateBindings.EditorApplication.AddUpdateFunc(OnEditorUpdate);
            }
        }

        /// <summary>
        /// Should serialization be disabled? This is used by the serialization migration system
        /// where after migration serialization should not happen automatically.
        /// </summary>
        [NonSerialized]
        public static bool DisableAutomaticSerialization = false;

        private static readonly List<ISerializedObject> s_pendingDeserializations = new List<ISerializedObject>();
        private static readonly List<ISerializedObject> s_pendingSerializations = new List<ISerializedObject>();
        private static readonly Dictionary<ISerializedObject, fiSerializedObjectSnapshot> s_snapshots = new Dictionary<ISerializedObject, fiSerializedObjectSnapshot>();

        public static HashSet<UnityObject> DirtyForceSerialize = new HashSet<UnityObject>();

        private static bool SupportsMultithreading<TSerializer>() where TSerializer : BaseSerializer {
            return
                fiSettings.ForceDisableMultithreadedSerialization == false &&
                fiUtility.IsUnity4 == false && // Too many things break in Unity 4 off the main thread that we won't even bother attempting
                fiSingletons.Get<TSerializer>().SupportsMultithreading;
        }

        /// <summary>
        /// Common logic for Awake() or OnEnable() methods inside of behaviors.
        /// </summary>
        public static void OnUnityObjectAwake<TSerializer>(ISerializedObject obj) where TSerializer : BaseSerializer {
            // No need to deserialize (possibly deserialized via OnUnityObjectDeserialize)
            if (obj.IsRestored) return;

            // Otherwise do a regular deserialization
            DoDeserialize(obj);
        }

        /// <summary>
        /// Common logic for ISerializationCallbackReceiver.OnDeserialize
        /// </summary>
        public static void OnUnityObjectDeserialize<TSerializer>(ISerializedObject obj) where TSerializer : BaseSerializer {
            if (SupportsMultithreading<TSerializer>()) {
                DoDeserialize(obj);
                return;
            }

            if (fiUtility.IsEditor == false) return;

            // We are in the editor so we have to use EditorApplication.update to linearize. This does not
            // necessarily mean we will actually deserialize the object, since if we are in play-mode we will
            // discard the EditorApplication.update linearization path in favor of the Awake() linearization
            // path since that is how the player will do it.
            lock (s_pendingDeserializations) {
                s_pendingDeserializations.Add(obj);
            }
        }

        /// <summary>
        /// Common logic for ISerializationCallbackReceiver.OnSerialize
        /// </summary>
        public static void OnUnityObjectSerialize<TSerializer>(ISerializedObject obj) where TSerializer : BaseSerializer {
            if (SupportsMultithreading<TSerializer>()) {
                DoSerialize(obj);
                return;
            }

            // BUG/FIXME: If (in a deployed player) the serializer does not support multithreaded serialization and we
            //            Instantiate an object with modifications, then those modifications will not get persisted.
            //            The Instantiated object will not be properly serialized - the user will need to manually call
            //            SaveState() on the behavior.

            if (fiUtility.IsEditor == false) return;

            // We have to run the serialization request on the Unity thread
            lock (s_pendingSerializations) {
                s_pendingSerializations.Add(obj);
            }
        }

        private static void OnEditorUpdate() {
            if (Application.isPlaying) {
                if (s_pendingDeserializations.Count > 0 || s_pendingSerializations.Count > 0 || s_snapshots.Count > 0) {
                    // Serialization / linearization will occur via Awake()

                    s_pendingDeserializations.Clear();
                    s_pendingSerializations.Clear();
                    s_snapshots.Clear();

                }
                //fiLateBindings.EditorApplication.RemUpdateFunc(OnEditorUpdate);
                return;
            }


            // Do not deserialize in the middle of a level load that might be running on another thread
            // (asynchronous) which can lead to a race condition causing the following assert:
            // ms_IDToPointer->find (obj->GetInstanceID ()) == ms_IDToPointer->end ()
            //
            // Very strange that the load is happening on another thread since OnEditorUpdate only
            // gets invoked from EditorApplication.update and EditorWindow.OnGUI.
            //
            // This method will get called again at a later point so there is no worries that we haven't
            // finished doing the deserializations.
            if (fiLateBindings.EditorApplication.isPlaying && Application.isLoadingLevel) {
                return;
            }

            while (s_pendingDeserializations.Count > 0) {
                ISerializedObject obj;
                lock (s_pendingDeserializations) {
                    obj = s_pendingDeserializations[s_pendingDeserializations.Count - 1];
                    s_pendingDeserializations.RemoveAt(s_pendingDeserializations.Count - 1);
                }

                // Check to make sure the object isn't destroyed.
                if (obj is UnityObject && ((UnityObject)obj) == null) continue;

                DoDeserialize(obj);
            }

            while (s_pendingSerializations.Count > 0) {
                ISerializedObject obj;
                lock (s_pendingSerializations) {
                    obj = s_pendingSerializations[s_pendingSerializations.Count - 1];
                    s_pendingSerializations.RemoveAt(s_pendingSerializations.Count - 1);
                }

                // Check to make sure the object isn't destroyed.
                if (obj is UnityObject && ((UnityObject)obj) == null) continue;

                DoSerialize(obj);
            }
        }

        private static void DoDeserialize(ISerializedObject obj) {
            obj.RestoreState();
        }

        private static void DoSerialize(ISerializedObject obj) {
            // If we have serialization disabled, then we *definitely* do not want to do anything.
            // Note: We put this check here for code clarity / robustness purposes. If this proves to be a
            //       perf issue, it can be hoisted outside of the top-level loop which invokes this method.
            if (DisableAutomaticSerialization) return;

            bool forceSerialize = obj is UnityObject && DirtyForceSerialize.Contains((UnityObject)obj);
            if (forceSerialize) DirtyForceSerialize.Remove((UnityObject)obj);

            // If this object is currently being inspected then we don't want to serialize it. This gives
            // a big perf boost. Note that we *do* want to serialize the object if we are entering play-mode
            // or compiling - otherwise a data loss will occur.
            if (forceSerialize == false &&
                obj is UnityObject &&
                fiLateBindings.EditorApplication.isCompilingOrChangingToPlayMode == false) {
                var toSerialize = (UnityObject)obj;
                if (toSerialize is Component) toSerialize = ((Component)toSerialize).gameObject;

                var selected = fiLateBindings.Selection.activeObject;
                if (selected is Component) selected = ((Component)selected).gameObject;

                if (ReferenceEquals(toSerialize, selected)) {
                    return;
                }
            }

            CheckForReset(obj);
            obj.SaveState();
        }

        private static HashSet<ISerializedObject> s_seen = new HashSet<ISerializedObject>();
        private static void CheckForReset(ISerializedObject obj) {
            // We don't want to send a reset notification for new objects which have no data. If we've already
            // seen an object and it has no data, then it was certainly reset.
            if (s_seen.Add(obj)) return;

            if (IsNullOrEmpty(obj.SerializedObjectReferences) && IsNullOrEmpty(obj.SerializedStateKeys) && IsNullOrEmpty(obj.SerializedStateValues)) {
                // Note: we do not clear out the keys; if we did, then we would not actually deserialize "null" onto them
                // Note: we call SaveState() so we can fetch the keys we need to deserialize
                obj.SaveState();
                for (int i = 0; i < obj.SerializedStateValues.Count; ++i) {
                    obj.SerializedStateValues[i] = null;
                }
                obj.RestoreState();

                fiRuntimeReflectionUtility.InvokeMethod(obj.GetType(), "Reset", obj, null);

                obj.SaveState();
            }
        }

        private static bool IsNullOrEmpty<T>(IList<T> list) {
            return list == null || list.Count == 0;
        }
    }
}
