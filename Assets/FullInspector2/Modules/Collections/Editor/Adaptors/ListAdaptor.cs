using System.Collections.Generic;
using FullInspector.Rotorz.ReorderableList;
using UnityEngine;

namespace FullInspector.Internal {
    /// <summary>
    /// Reorderable list adapter for generic list.
    /// </summary>
    /// <remarks>
    /// <para>This adapter can be subclassed to add special logic to item height calculation. You
    /// may want to implement a custom adapter class where specialized functionality is
    /// needed.</para>
    /// </remarks>
    public class ListAdaptor<T> : IReorderableListAdaptor {
        public delegate float ItemHeight(T item, fiGraphMetadataChild metadata);
        public delegate T ItemDrawer(Rect position, T item, fiGraphMetadataChild metadata);

        private ItemHeight _itemHeight;
        private ItemDrawer _itemDrawer;
        private fiGraphMetadata _metadata;
        private IList<T> _list;

        private static T DefaultItemGenerator() {
            return default(T);
        }

        /// <param name="list">The list which can be reordered.</param>
        /// <param name="itemDrawer">Callback to draw list item.</param>
        /// <param name="itemHeight">Height of list item in pixels.</param>
        public ListAdaptor(IList<T> list, ItemDrawer itemDrawer, ItemHeight itemHeight, fiGraphMetadata metadata) {
            _metadata = metadata;
            _list = list;
            _itemDrawer = itemDrawer;
            _itemHeight = itemHeight;
        }

        public int Count {
            get { return _list.Count; }
        }
        public virtual bool CanDrag(int index) {
            return true;
        }
        public virtual bool CanRemove(int index) {
            return true;
        }
        public void Add() {
            T item = DefaultItemGenerator();
            _list.Add(item);
        }
        public void Insert(int index) {
            Add();

            // shift metadata forwards
            for (int i = _list.Count - 1; i > index; --i) {
                _list[i] = _list[i - 1];
                _metadata.SetChild(i, _metadata.Enter(i - 1).Metadata);
            }

            // update the reference at index
            _list[index] = default(T);
            _metadata.SetChild(index, new fiGraphMetadata());
        }

        public void Duplicate(int index) {
            T current = _list[index];
            Insert(index);
            _list[index] = current;
        }
        public void Remove(int index) {
            // shift elements back
            for (int i = index; i < _list.Count - 1; ++i) {
                _metadata.SetChild(i, _metadata.Enter(i + 1).Metadata);
            }
            _list.RemoveAt(index);
        }
        public void Move(int sourceIndex, int destIndex) {
            if (destIndex > sourceIndex)
                --destIndex;

            T item = _list[sourceIndex];
            fiGraphMetadata itemMetadata = _metadata.Enter(sourceIndex).Metadata;

            Remove(sourceIndex);
            Insert(destIndex);

            _list[destIndex] = item;
            _metadata.SetChild(destIndex, itemMetadata);
        }

        public void Clear() {
            _list.Clear();
        }

        public virtual void DrawItem(Rect position, int index) {
            // Rotorz seems to sometimes give an index of -1, not sure why.
            if (index < 0) {
                return;
            }

            var metadata = _metadata.Enter(index);
            fiGraphMetadataCallbacks.ListMetadataCallback(metadata.Metadata, fiGraphMetadataCallbacks.Cast(_list), index);

            var updatedItem = _itemDrawer(position, _list[index], metadata);
            var existingItem = _list[index];

            if (existingItem == null ||
                existingItem.Equals(updatedItem) == false) {
                _list[index] = updatedItem;
            }
        }
        public virtual float GetItemHeight(int index) {
            var metadata = _metadata.Enter(index);
            fiGraphMetadataCallbacks.ListMetadataCallback(metadata.Metadata, fiGraphMetadataCallbacks.Cast(_list), index);
            return _itemHeight(_list[index], metadata);
        }
    }
}