using System;
using System.Collections.Generic;
using System.Linq;
using FullInspector.Internal;
using FullSerializer;
using UnityObject = UnityEngine.Object;

namespace FullInspector {
    /// <summary>
    /// Interface for a type that is able to provide a persistent metadata instance.
    /// </summary>
    public interface fiIPersistentMetadataProvider {
        /// <summary>
        /// Run any initialization code for the provider.
        /// </summary>
        void RestoreData(UnityObject target);

        /// <summary>
        /// Reset any stored metadata.
        /// </summary>
        void Reset(UnityObject target);

        /// <summary>
        /// Return the type of metadata this provider supports.
        /// </summary>
        Type MetadataType { get; }
    }

    public static class fiPersistentMetadata {
        #region Metadata Providers
        private static readonly fiIPersistentMetadataProvider[] s_providers;
        static fiPersistentMetadata() {
            s_providers = fiRuntimeReflectionUtility.GetAssemblyInstances<fiIPersistentMetadataProvider>().ToArray();
            for (int i = 0; i < s_providers.Length; ++i) {
                fiLog.Log(typeof(fiPersistentMetadata), "Using provider {0} to support metadata of type {1}",
                    s_providers[i].GetType().CSharpName(), s_providers[i].MetadataType.CSharpName());
            }
        }
        #endregion

        private static Dictionary<fiUnityObjectReference, fiGraphMetadata> s_metadata = new Dictionary<fiUnityObjectReference, fiGraphMetadata>();
        public static fiGraphMetadata GetMetadataFor(UnityObject target_) {
            var target = new fiUnityObjectReference(target_);
            fiGraphMetadata metadata;
            if (s_metadata.TryGetValue(target, out metadata) == false) {
                // Make sure that we update the s_metadata instance for target before initializing all of the providers,
                // as some of the providers may recurisvely call into this method to fetch the actual fiGraphMetadata
                // instance during initialization.
                metadata = new fiGraphMetadata(target);
                s_metadata[target] = metadata;
                for (int i = 0; i < s_providers.Length; ++i) {
                    s_providers[i].RestoreData(target.Target);
                }
            }
            return metadata;
        }

        public static void Reset(UnityObject target_) {
            var target = new fiUnityObjectReference(target_);
            if (s_metadata.ContainsKey(target)) {
                s_metadata.Remove(target);

                for (int i = 0; i < s_providers.Length; ++i) {
                    s_providers[i].Reset(target.Target);
                }
            }
        }
    }
}