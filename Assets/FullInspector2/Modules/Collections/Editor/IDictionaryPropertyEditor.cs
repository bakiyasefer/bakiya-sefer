using System;
using System.Collections.Generic;
using System.Reflection;
using FullInspector;
using FullInspector.Internal;
using FullInspector.Rotorz.ReorderableList;

namespace FullSerializer.Internal {
    // special one for dictionaries so we only edit the key in the add region
    [CustomPropertyEditor(typeof(IDictionary<,>), Inherit = true)]
    public class IDictionaryPropertyEditor<TActual, TKey, TValue> : BaseCollectionPropertyEditor<TActual, IDictionary<TKey, TValue>, KeyValuePair<TKey, TValue>, TKey> {
        public IDictionaryPropertyEditor(Type editedType, ICustomAttributeProvider attributes)
            : base(editedType, attributes) {
        }

        protected override IReorderableListAdaptor GetAdaptor(IDictionary<TKey, TValue> collection, fiGraphMetadata metadata) {
            return new CollectionAdaptor<KeyValuePair<TKey, TValue>>(collection, DrawItem, GetItemHeight, metadata);
        }

        protected override void AddItemToCollection(TKey item, ref IDictionary<TKey, TValue> collection, IReorderableListAdaptor adaptor) {
            if (!collection.ContainsKey(item)) {
                //(adaptor as CollectionAdaptor<KeyValuePair<TKey, TValue>>).Add(new KeyValuePair<TKey, TValue>(item, default(TValue)));
                collection.Add(item, default(TValue));
            }
        }

        protected override bool AllowReordering {
            get { return false; }
        }
    }
}