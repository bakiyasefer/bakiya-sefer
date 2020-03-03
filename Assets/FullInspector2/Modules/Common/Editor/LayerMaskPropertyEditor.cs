using System;
using UnityEditor;
using UnityEngine;

namespace FullInspector.Modules.Common {
    // TODO: For some reason the standard PropertyDrawer integration does not work with LayerMask
    //       It seems like GUI.changed is not being triggered correctly

    public class LayerMaskContainer : MonoBehaviour {
        public LayerMask Value;
    }

    public class LayerMaskMetadata : IGraphMetadataItemNotPersistent {
        [NonSerialized]
        public LayerMaskContainer Container;
    }

    [CustomPropertyEditor(typeof(LayerMask))]
    public class LayerMaskEditor : PropertyEditor<LayerMask> {
        // see http://answers.unity3d.com/questions/436295/how-to-have-a-gradient-editor-in-an-editor-script.html
        // for the inspiration behind this approach

        LayerMask DoField(LayerMaskMetadata metadata, Rect region, GUIContent label, LayerMask mask) {
            // I don't really understand why, but setting metadata.Value to mask causes everything to
            // break. It looks like Unity doesn't so SerializedProperty value changing in one edit cycle,
            // but rather across a set of events.

            var serializedObject = new SerializedObject(metadata.Container);
            var serializedProp = serializedObject.FindProperty("Value");

            EditorGUI.PropertyField(region, serializedProp, label);
            serializedObject.ApplyModifiedProperties();

            if (mask.value != serializedProp.intValue) {
                GUI.changed = true;
            }

            return new LayerMask { value = serializedProp.intValue };
        }

        public override LayerMask Edit(Rect region, GUIContent label, LayerMask element, fiGraphMetadata metadata) {
            var container = metadata.GetMetadata<LayerMaskMetadata>();

            // note: we delay construction of the GameObject because when the metadata is
            //       deserialized, we don't want to construct a new GameObject in another thread
            if (container.Container == null) {
                var obj = (GameObject)EditorUtility.CreateGameObjectWithHideFlags(
                    "ProxyLayerMaskEditor", HideFlags.HideInHierarchy | HideFlags.DontSave);

                container.Container = obj.AddComponent<LayerMaskContainer>();
                container.Container.Value = element;
            }

            return DoField(container, region, label, element);
        }

        public override float GetElementHeight(GUIContent label, LayerMask element, fiGraphMetadata metadata) {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}