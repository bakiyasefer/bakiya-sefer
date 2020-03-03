using System;
using System.Collections.Generic;
using FullInspector.Internal;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace FullInspector {
    [AddComponentMenu("")]
    public abstract class fiBaseStorageComponent<T> : MonoBehaviour, fiIEditorOnlyTag
#if !UNITY_4_3
        , ISerializationCallbackReceiver 
#endif
        {

#if !UNITY_4_3
        [SerializeField]
        private List<UnityObject> _keys;
        [SerializeField]
        private List<T> _values;
#endif

        private IDictionary<UnityObject, T> _data;
        public IDictionary<UnityObject, T> Data {
            get {
                if (_data == null) _data = new Dictionary<UnityObject, T>();
                return _data;
            }
        }

#if !UNITY_4_3
        void ISerializationCallbackReceiver.OnAfterDeserialize() {
            if (_keys == null || _values == null) return;

            _data = new Dictionary<UnityObject, T>();
            for (int i = 0; i < Math.Min(_keys.Count, _values.Count); ++i) {
                if (ReferenceEquals(_keys[i], null) == false) {
                    Data[_keys[i]] = _values[i];
                }
            }
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() {
            if (_data == null) {
                _keys = null;
                _values = null;
                return;
            }

            _keys = new List<UnityObject>(_data.Count);
            _values = new List<T>(_data.Count);
            foreach (var entry in _data) {
                // We do *not* check to see if entry.Key refers to a valid UnityObject here, as
                // the GetInstanceId() restoration mechanism will not work properly.
                _keys.Add(entry.Key);
                _values.Add(entry.Value);
            }
        }
#endif
    }
}