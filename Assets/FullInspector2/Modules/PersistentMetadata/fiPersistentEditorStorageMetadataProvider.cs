using System;
using UnityObject = UnityEngine.Object;

namespace FullInspector.Internal {
    public interface fiIGraphMetadataStorage {
        void RestoreData(UnityObject target);
    }

    public abstract class fiPersistentEditorStorageMetadataProvider<TItem, TStorage> : fiIPersistentMetadataProvider
        where TItem : new()
        where TStorage : fiIGraphMetadataStorage, new() {

        public void RestoreData(UnityObject target) {
            fiPersistentEditorStorage.Read<TStorage>(target).RestoreData(target);
        }

        public void Reset(UnityObject target) {
            fiPersistentEditorStorage.Reset<TStorage>(target);
        }

        public Type MetadataType {
            get { return typeof(TItem); }
        }
    }
}