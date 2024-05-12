using System.Collections;
using System.Diagnostics;

namespace WikiClientLibrary.Wikibase.Infrastructures;

/// <summary>
/// An unordered dictionary whose keys lies in the items.
/// </summary>
/// <remarks>The key cannot be <c>null</c> in the collection.</remarks>
/// <typeparam name="TKey">Type of the key.</typeparam>
/// <typeparam name="TItem">Type of the item.</typeparam>
[DebuggerDisplay("Count = {Count}")]
[DebuggerTypeProxy(typeof(UnorderedKeyedCollection<,>.DebugView))]
public abstract class UnorderedKeyedCollection<TKey, TItem> : ICollection<TItem>, ICollection where TKey : notnull
{

    private readonly Dictionary<TKey, TItem> dict;
    private bool _IsReadOnly;

    public UnorderedKeyedCollection() : this(EqualityComparer<TKey>.Default)
    {
    }

    public UnorderedKeyedCollection(IEqualityComparer<TKey> keyComparer)
    {
        dict = new Dictionary<TKey, TItem>(keyComparer);
    }

    protected abstract TKey GetKeyForItem(TItem item);

    protected void ChangeItemKey(TItem item, TKey newKey)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        if (newKey == null) throw new ArgumentNullException(nameof(newKey));
        AssertMutable();
        var oldKey = GetKeyForItem(item);
        if (!dict.TryGetValue(oldKey, out var dictItem)
            || !EqualityComparer<TItem>.Default.Equals(item, dictItem))
            throw new ArgumentException("Item is not in the collection.", nameof(item));
        if (dict.Comparer.Equals(oldKey, newKey)) return;
        dict.Add(newKey, item);
        dict.Remove(oldKey);
    }

    public TItem this[TKey key] => dict[key];

    public bool ContainsKey(TKey key) => dict.ContainsKey(key);

    /// <inheritdoc />
    public IEnumerator<TItem> GetEnumerator()
    {
        return dict.Values.GetEnumerator();
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
        dict.Add(key, item);
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
        return dict.TryGetValue(key, out var value) && Equals(value, item);
    }

    /// <inheritdoc />
    public void CopyTo(TItem[] array, int arrayIndex)
    {
        dict.Values.CopyTo(array, arrayIndex);
    }

    /// <inheritdoc />
    public virtual bool Remove(TItem item)
    {
        AssertMutable();
        var key = GetKeyForItem(item);
        return dict.TryGetValue(key, out var value)
               && EqualityComparer<TItem>.Default.Equals(value, item)
               && dict.Remove(key);
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
    public int Count => dict.Count;

    /// <inheritdoc />
    public bool IsReadOnly
    {
        get { return _IsReadOnly; }
        set
        {
            if (value)
                _IsReadOnly = true;
            else
                AssertMutable();
        }
    }

    protected void AssertMutable()
    {
        if (_IsReadOnly) throw new NotSupportedException("The dictionary is read-only.");
    }

    private sealed class DebugView
    {
        private readonly UnorderedKeyedCollection<TKey, TItem> source;

        public DebugView(UnorderedKeyedCollection<TKey, TItem> source)
        {
            Debug.Assert(source != null);
            this.source = source;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public KeyValuePair<TKey, TItem>[] Items => source.dict.ToArray();
    }

}