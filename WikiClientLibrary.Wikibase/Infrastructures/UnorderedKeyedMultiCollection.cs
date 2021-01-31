using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace WikiClientLibrary.Wikibase.Infrastructures
{
    /// <summary>
    /// An unordered 1:n dictionary whose keys lies in the items.
    /// </summary>
    /// <remarks>The key cannot be <c>null</c> in the collection.</remarks>
    /// <typeparam name="TKey">Type of the key.</typeparam>
    /// <typeparam name="TItem">Type of the item.</typeparam>
    [DebuggerDisplay("Keys.Count = {Keys.Count}, Count = {Count}")]
    [DebuggerTypeProxy(typeof(UnorderedKeyedMultiCollection<, >.DebugView))]
    public abstract class UnorderedKeyedMultiCollection<TKey, TItem> : ICollection<TItem>, ICollection
    {

        private readonly Dictionary<TKey, Slot> dict;
        private bool _IsReadOnly;

        public UnorderedKeyedMultiCollection() : this(EqualityComparer<TKey>.Default)
        {
        }

        public UnorderedKeyedMultiCollection(IEqualityComparer<TKey> keyComparer)
        {
            dict = new Dictionary<TKey, Slot>(keyComparer);
        }

        private Slot GetOrCreateSlotFor(TKey key)
        {
            Debug.Assert(key != null);
            Debug.Assert(!_IsReadOnly);
            if (dict.TryGetValue(key, out var c)) return c;
            c = new Slot(new List<TItem>());
            dict.Add(key, c);
            return c;
        }

        protected abstract TKey GetKeyForItem(TItem item);

        protected void ChangeItemKey(TItem item, TKey newKey)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (newKey == null) throw new ArgumentNullException(nameof(newKey));
            AssertMutable();
            var oldKey = GetKeyForItem(item);
            if (!dict.TryGetValue(oldKey, out var oldSlot) || !oldSlot.Items.Contains(item))
                throw new ArgumentException("Item is not in the collection.", nameof(item));
            if (dict.Comparer.Equals(oldKey, newKey)) return;
            var newSlot = GetOrCreateSlotFor(newKey);
            newSlot.Items.Add(item);
            oldSlot.Items.Remove(item);
            if (oldSlot.Items.Count == 0) dict.Remove(oldKey);
        }

        /// <summary>
        /// Gets a read-only view of the key collection.
        /// </summary>
        public ICollection<TKey> Keys => dict.Keys;

        /// <summary>
        /// Gets a read-only view of the items with the specified key.
        /// </summary>
        /// <returns>The items with specified key, or empty collection if no such item exists.
        /// The view is guaranteed to be valid until any modifications to the collection.</returns>
        public ICollection<TItem> this[TKey key]
        {
            get
            {
                if (key == null) throw new ArgumentNullException(nameof(key));
                if (dict.TryGetValue(key, out var slot)) return slot.ReadOnlyItems;
                return Array.Empty<TItem>();
            }
        }

        public bool ContainsKey(TKey key) => dict.ContainsKey(key);

        protected IEnumerable<TItem> EnumItems()
        {
            foreach (var p in dict)
            {
                foreach (var i in p.Value.Items)
                {
                    yield return i;
                }
            }
        }

        /// <inheritdoc />
        public IEnumerator<TItem> GetEnumerator()
        {
            return EnumItems().GetEnumerator();
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <inheritdoc />
        public virtual void Add(TItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            AssertMutable();
            var key = GetKeyForItem(item);
            var slot = GetOrCreateSlotFor(key);
            slot.Items.Add(item);
        }

        /// <inheritdoc />
        public virtual void Clear()
        {
            AssertMutable();
            dict.Clear();
        }

        /// <inheritdoc />
        public bool Contains(TItem item)
        {
            if (item == null) return false;
            var key = GetKeyForItem(item);
            return dict.TryGetValue(key, out var value) && value.Items.Contains(item);
        }

        /// <inheritdoc />
        public void CopyTo(TItem[] array, int arrayIndex)
        {
            foreach (var item in EnumItems())
            {
                array[arrayIndex] = item;
            }
        }

        /// <summary>
        /// Removes all the items with specified key.
        /// </summary>
        /// <returns>Whether one or more items has been removed.</returns>
        public virtual bool Remove(TKey key)
        {
            AssertMutable();
            return dict.Remove(key);
        }

        /// <inheritdoc />
        public virtual bool Remove(TItem item)
        {
            AssertMutable();
            var key = GetKeyForItem(item);
            if (dict.TryGetValue(key, out var slot) && dict.Remove(key))
            {
                if (slot.Items.Count == 0) dict.Remove(key);
                return true;
            }
            return false;
        }

        /// <inheritdoc />
        public void CopyTo(Array array, int index)
        {
            ((ICollection)dict.Values).CopyTo(array, index);
        }

        /// <inheritdoc />
        bool ICollection.IsSynchronized => false;

        /// <inheritdoc />
        object ICollection.SyncRoot => ((ICollection)dict).SyncRoot;

        /// <inheritdoc cref="ICollection{T}.Count" />
        public int Count => dict.Sum(p => p.Value.Items.Count);

        /// <inheritdoc />
        public bool IsReadOnly
        {
            get { return _IsReadOnly; }
            set
            {
                AssertMutable();
                _IsReadOnly = value;
            }
        }

        protected void AssertMutable()
        {
            if (_IsReadOnly) throw new NotSupportedException("The dictionary is read-only.");
        }

        private struct Slot
        {
            public Slot(List<TItem> items)
            {
                Debug.Assert(items != null);
                Items = items;
                ReadOnlyItems = new ReadOnlyCollection<TItem>(items);
            }

            public List<TItem> Items { get; }

            public ReadOnlyCollection<TItem> ReadOnlyItems { get; }

        }

        private sealed class DebugView
        {
            private readonly UnorderedKeyedMultiCollection<TKey, TItem> source;

            public DebugView(UnorderedKeyedMultiCollection<TKey, TItem> source)
            {
                Debug.Assert(source != null);
                this.source = source;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public KeyValuePair<TKey, TItem>[] Items
            {
                get
                {
                    var buffer = new KeyValuePair<TKey, TItem>[source.Count];
                    int index = 0;
                    foreach (var p in source.dict)
                    {
                        foreach (var item in p.Value.Items)
                        {
                            buffer[index] = new KeyValuePair<TKey, TItem>(p.Key, item);
                            index++;
                        }
                    }
                    return buffer;
                }
            }
        }

    }
}
