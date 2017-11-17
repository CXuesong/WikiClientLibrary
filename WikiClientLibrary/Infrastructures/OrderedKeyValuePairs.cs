using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WikiClientLibrary.Infrastructures
{

    /// <summary>
    /// A sequence of ordered key-value pairs. The keys can duplicate with each other.
    /// </summary>
    public class OrderedKeyValuePairs<TKey, TValue> : Collection<KeyValuePair<TKey, TValue>>
    {
        public OrderedKeyValuePairs() : this(null)
        {
        }

        public OrderedKeyValuePairs(IEqualityComparer<TKey> keyComparer)
        {
            KeyComparer = keyComparer ?? EqualityComparer<TKey>.Default;
        }

        public IEqualityComparer<TKey> KeyComparer { get; }

        public void Add(TKey key, TValue value)
        {
            Add(new KeyValuePair<TKey, TValue>(key, value));
        }

        public void AddRange(IEnumerable<KeyValuePair<TKey, TValue>> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            foreach (var item in items) Add(item);
        }

        public TValue this[TKey key]
        {
            get
            {
                foreach (var p in Items)
                {
                    if (KeyComparer.Equals(p.Key, key)) return p.Value;
                }
                throw new KeyNotFoundException();
            }
            set
            {
                for (var i = 0; i < Items.Count; i++)
                {
                    var p = Items[i];
                    if (KeyComparer.Equals(p.Key, key))
                        Items[i] = new KeyValuePair<TKey, TValue>(key, value);
                    return;
                }
                Items.Add(new KeyValuePair<TKey, TValue>(key, value));
            }
        }

    }
}
