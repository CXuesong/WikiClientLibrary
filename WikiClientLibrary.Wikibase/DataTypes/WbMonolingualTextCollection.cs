using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace WikiClientLibrary.Wikibase.DataTypes
{

    /// <summary>
    /// Provides convenient access to 1:1 language-text pairs of multilingual text.
    /// </summary>
    /// <remarks>All the language codes are normalized to lower-case and are case-insensitive.</remarks>
    /// <seealso cref="WbMonolingualTextsCollection"/>
    /// <seealso cref="WbMonolingualText"/>
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof(DebugView))]
    public class WbMonolingualTextCollection : ICollection<WbMonolingualText>
    {

        private readonly Dictionary<string, string> dict
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private bool _IsReadOnly;

        public WbMonolingualTextCollection()
        {
            
        }
        
        public WbMonolingualTextCollection(IEnumerable<WbMonolingualText> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            foreach (var i in items)
            {
                Add(i);
            }
        }

        public void Add(string language, string text)
        {
            if (language == null) throw new ArgumentNullException(nameof(language));
            if (text == null) throw new ArgumentNullException(nameof(text));
            AssertMutable();
            language = language.ToLowerInvariant();
            dict.Add(language, text);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentException"><paramref name="item"/> is <see cref="WbMonolingualText.Null"/>.</exception>
        public void Add(WbMonolingualText item)
        {
            if (item == WbMonolingualText.Null)
                throw new ArgumentException("Cannot add Null value into the collection.", nameof(item));
            Add(item.Language, item.Text);
        }

        public bool Remove(string language)
        {
            if (language == null) throw new ArgumentNullException(nameof(language));
            AssertMutable();
            return dict.Remove(language);
        }

        /// <inheritdoc />
        public void Clear()
        {
            AssertMutable();
            dict.Clear();
        }

        /// <summary>
        /// Gets/sets the associated text to the specified language.
        /// </summary>
        /// <param name="language">The desired language code.</param>
        /// <exception cref="ArgumentNullException"><paramref name="language"/> is <c>null</c>.</exception>
        /// <returns>The text associated to the language,
        /// or <c>null</c> if the language does not have any associated text.</returns>
        public string this[string language]
        {
            get
            {
                if (language == null) throw new ArgumentNullException(nameof(language));
                return dict.TryGetValue(language, out var text) ? text : null;
            }
            set
            {
                if (language == null) throw new ArgumentNullException(nameof(language));
                AssertMutable();
                if (value == null) dict.Remove(language);
                else dict[language] = value;
            }
        }

        /// <summary>
        /// Tries to get the <see cref="WbMonolingualText"/> associated with the specified language.
        /// </summary>
        /// <param name="language">The desired language code.</param>
        /// <exception cref="ArgumentNullException"><paramref name="language"/> is <c>null</c>.</exception>
        /// <returns>The desired <see cref="WbMonolingualText"/>,
        /// or <see cref="WbMonolingualText.Null"/> if the language does not have any associated text.</returns>
        public WbMonolingualText TryGetMonolingualText(string language)
        {
            if (language == null) throw new ArgumentNullException(nameof(language));
            return dict.TryGetValue(language, out var text)
                ? new WbMonolingualText(language, text)
                : WbMonolingualText.Null;
        }

        /// <summary>
        /// Adds or updates a <see cref="WbMonolingualText"/> item by language code.
        /// </summary>
        /// <param name="item">The new item to add or update to.</param>
        /// <exception cref="ArgumentException"><paramref name="item"/> is <see cref="WbMonolingualText.Null"/>.</exception>
        public void Set(WbMonolingualText item)
        {
            if (item == WbMonolingualText.Null)
                throw new ArgumentException("Cannot add Null value into the collection.", nameof(item));
            AssertMutable();
            dict[item.Language] = item.Text;
        }

        /// <inheritdoc />
        public int Count => dict.Count;

        public bool ContainsLanguage(string language)
        {
            return dict.ContainsKey(language);
        }

        /// <inheritdoc />
        public IEnumerator<WbMonolingualText> GetEnumerator()
        {
            return dict.Select(p => new WbMonolingualText(p.Key, p.Value, true)).GetEnumerator();
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <inheritdoc />
        bool ICollection<WbMonolingualText>.Contains(WbMonolingualText item)
        {
            return dict.TryGetValue(item.Language, out var text) && text == item.Text;
        }

        /// <inheritdoc />
        void ICollection<WbMonolingualText>.CopyTo(WbMonolingualText[] array, int arrayIndex)
        {
            foreach (var p in dict)
            {
                array[arrayIndex] = new WbMonolingualText(p.Key, p.Value, true);
                arrayIndex++;
            }
        }

        /// <inheritdoc />
        bool ICollection<WbMonolingualText>.Remove(WbMonolingualText item)
        {
            if (item == WbMonolingualText.Null) return false;
            AssertMutable();
            if (dict.TryGetValue(item.Language, out var text) && text == item.Text)
            {
                dict.Remove(item.Language);
                return true;
            }
            return false;
        }

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

        private void AssertMutable()
        {
            if (_IsReadOnly) throw new NotSupportedException("The dictionary is read-only.");
        }

        private sealed class DebugView
        {
            private readonly WbMonolingualTextCollection source;

            public DebugView(WbMonolingualTextCollection source)
            {
                Debug.Assert(source != null);
                this.source = source;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public WbMonolingualText[] Items => source.ToArray();
        }

    }

    /// <summary>
    /// Provides convenient access to 1:n language-text pairs of multilingual text.
    /// </summary>
    /// <remarks>All the language codes are normalized to lower-case and are case-insensitive.</remarks>
    /// <seealso cref="WbMonolingualText"/>
    [DebuggerDisplay("Languages.Count = {Languages.Count}, Count = {Count}")]
    [DebuggerTypeProxy(typeof(DebugView))]
    public class WbMonolingualTextsCollection : ICollection<WbMonolingualText>
    {

        private readonly Dictionary<string, HashSet<string>> dict
            = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private bool _IsReadOnly;

        public WbMonolingualTextsCollection()
        {

        }

        public WbMonolingualTextsCollection(IEnumerable<WbMonolingualText> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            foreach (var i in items)
            {
                Add(i);
            }
        }

        public WbMonolingualTextsCollection(IEnumerable<KeyValuePair<string, IEnumerable<string>>> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            foreach (var p in items)
            {
                if (p.Key == null)
                    throw new ArgumentException("The collection contains null language code.", nameof(items));
                if (p.Value == null)
                    throw new ArgumentException("The collection contains null text collection.", nameof(items));
                var slot = GetOrCreateSlotFor(p.Key);
                foreach (var text in p.Value) slot.Add(text);
                if (slot.Count == 0) dict.Remove(p.Key);
            }
        }

        private HashSet<string> GetOrCreateSlotFor(string language)
        {
            Debug.Assert(language != null);
            Debug.Assert(!_IsReadOnly);
            if (dict.TryGetValue(language, out var c)) return c;
            c = new HashSet<string>();
            dict.Add(language.ToLowerInvariant(), c);
            return c;
        }

        public bool Add(string language, string text)
        {
            if (language == null) throw new ArgumentNullException(nameof(language));
            if (text == null) throw new ArgumentNullException(nameof(text));
            AssertMutable();
            return GetOrCreateSlotFor(language).Add(text);
        }

        /// <exception cref="ArgumentException"><paramref name="item"/> is <see cref="WbMonolingualText.Null"/>.</exception>
        public bool Add(WbMonolingualText item)
        {
            if (item == WbMonolingualText.Null)
                throw new ArgumentException("Cannot add Null value into the collection.", nameof(item));
            return Add(item.Language, item.Text);
        }

        /// <inheritdoc />
        void ICollection<WbMonolingualText>.Add(WbMonolingualText item)
        {
            Add(item);
        }

        /// <summary>
        /// Removes all the text entries associated with the specified language from the collection.
        /// </summary>
        /// <param name="language">The language code to be removed.</param>
        /// <exception cref="ArgumentNullException"><paramref name="language"/> is <c>null</c>.</exception>
        /// <returns>Whether one or more text entries has been removed.</returns>
        public bool Remove(string language)
        {
            if (language == null) throw new ArgumentNullException(nameof(language));
            AssertMutable();
            return dict.Remove(language);
        }

        /// <summary>
        /// Removes a text entry from the collection.
        /// </summary>
        /// <param name="language">The language code to be removed.</param>
        /// <param name="text">The text content of the entry to be removed.</param>
        /// <returns>Whether one or more text entries has been removed.</returns>
        public bool Remove(string language, string text)
        {
            if (language == null || text == null) return false;
            AssertMutable();
            if (dict.TryGetValue(language, out var slot) && slot.Remove(text))
            {
                if (slot.Count == 0) dict.Remove(language);
                return true;
            }
            return false;
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentException"><paramref name="item"/> is <see cref="WbMonolingualText.Null"/>.</exception>
        public bool Remove(WbMonolingualText item)
        {
            if (item == WbMonolingualText.Null) return false;
            return Remove(item.Language, item.Text);
        }

        /// <inheritdoc />
        public void Clear()
        {
            AssertMutable();
            dict.Clear();
        }

        /// <summary>
        /// Gets a view of all the languages.
        /// </summary>
        public ICollection<string> Languages => dict.Keys;

        /// <summary>
        /// Gets/sets the associated text to the specified language.
        /// </summary>
        /// <param name="language">The desired language code.</param>
        /// <exception cref="ArgumentNullException"><paramref name="language"/> is <c>null</c>.</exception>
        /// <returns>The text associated to the language,
        /// or <c>null</c> if the language does not have any associated text.</returns>
        public IEnumerable<string> this[string language]
        {
            get
            {
                if (language == null) throw new ArgumentNullException(nameof(language));
                return dict.TryGetValue(language, out var slot) ? slot : Enumerable.Empty<string>();
            }
            set
            {
                if (language == null) throw new ArgumentNullException(nameof(language));
                AssertMutable();
                if (value == null)
                {
                    dict.Remove(language);
                }
                else if (value is ICollection collection && collection.Count == 0)
                {
                    dict.Remove(language);
                }
                else
                {
                    var slot = GetOrCreateSlotFor(language);
                    slot.Clear();
                    foreach (var i in value) slot.Add(i);
                    if (slot.Count == 0) dict.Remove(language);
                }
            }
        }

        /// <summary>
        /// Tries to get the <see cref="WbMonolingualText"/> associated with the specified language.
        /// </summary>
        /// <param name="language">The desired language code.</param>
        /// <exception cref="ArgumentNullException"><paramref name="language"/> is <c>null</c>.</exception>
        /// <returns>The desired <see cref="WbMonolingualText"/>,
        /// or <see cref="WbMonolingualText.Null"/> if the language does not have any associated text.</returns>
        public IEnumerable<WbMonolingualText> TryGetMonolingualTexts(string language)
        {
            if (language == null) throw new ArgumentNullException(nameof(language));
            language = language.ToLowerInvariant();
            return dict.TryGetValue(language, out var slot)
                ? slot.Select(text => new WbMonolingualText(language, text, true))
                : Enumerable.Empty<WbMonolingualText>();
        }

        /// <inheritdoc />
        public int Count => dict.Values.Sum(slot => slot.Count);

        public bool ContainsLanguage(string language)
        {
            return dict.ContainsKey(language);
        }

        private IEnumerable<WbMonolingualText> EnumItems()
        {
            foreach (var p in dict)
            {
                foreach (var text in p.Value)
                {
                    yield return new WbMonolingualText(p.Key, text, true);
                }
            }
        }

        /// <inheritdoc />
        public IEnumerator<WbMonolingualText> GetEnumerator()
        {
            return EnumItems().GetEnumerator();
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <inheritdoc />
        bool ICollection<WbMonolingualText>.Contains(WbMonolingualText item)
        {
            return dict.TryGetValue(item.Language, out var slot) && slot.Contains(item.Text);
        }

        /// <inheritdoc />
        void ICollection<WbMonolingualText>.CopyTo(WbMonolingualText[] array, int arrayIndex)
        {
            foreach (var i in EnumItems())
            {
                array[arrayIndex] = i;
                arrayIndex++;
            }
        }

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

        private void AssertMutable()
        {
            if (_IsReadOnly) throw new NotSupportedException("The dictionary is read-only.");
        }

        private sealed class DebugView
        {
            private readonly WbMonolingualTextsCollection source;

            public DebugView(WbMonolingualTextsCollection source)
            {
                Debug.Assert(source != null);
                this.source = source;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public WbMonolingualText[] Items => source.ToArray();
        }

    }

}
