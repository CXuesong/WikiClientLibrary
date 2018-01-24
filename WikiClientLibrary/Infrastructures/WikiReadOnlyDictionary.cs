using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using WikiClientLibrary.Pages;

namespace WikiClientLibrary.Infrastructures
{
    /// <summary>
    /// A dictionary with predefined strong-typed derived properties customizable by implementers.
    /// </summary>
    [JsonDictionary]
    public class WikiReadOnlyDictionary : IDictionary<string, JToken>
    {

        private readonly IDictionary<string, JToken> myDict = new Dictionary<string, JToken>();
        private bool _IsReadOnly = false;

        protected void MakeReadonly()
        {
            _IsReadOnly = true;
        }

        /// <summary>
        /// Gets the count of all properties.
        /// </summary>
        public int Count => myDict.Count;

        /// <summary>
        /// Tries to get the value of the specified property.
        /// </summary>
        /// <param name="key">The property name.</param>
        /// <returns>A clone of property value, OR <c>null</c> if such property cannot be found.</returns>
        public JToken this[string key]
        {
            get
            {
                var value = GetValueDirect(key);
                if (_IsReadOnly) value = value?.DeepClone();
                return value;
            }
            set
            {
                if (_IsReadOnly) throw new NotSupportedException("Collection is read-only.");
                myDict[key] = value;
            }
        }

        /// <summary>
        /// Tries to directly gets the value of the specified property.
        /// </summary>
        protected JToken GetValueDirect(string key)
        {
            if (myDict.TryGetValue(key, out var value)) return value;
            return null;
        }

        /// <inheritdoc />
        public ICollection<string> Keys => myDict.Keys;

        /// <inheritdoc />
        bool IDictionary<string, JToken>.Remove(string key)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public bool ContainsKey(string key)
        {
            return myDict.ContainsKey(key);
        }

        /// <inheritdoc />
        public bool TryGetValue(string key, out JToken value)
        {
            if (myDict.TryGetValue(key, out var v))
            {
                value = v.DeepClone();
                return true;
            }
            value = null;
            return false;
        }

        /// <inheritdoc />
        public ICollection<JToken> Values => myDict.Values;

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            MakeReadonly();
        }

        #region Explicit Interface Implementations

        /// <inheritdoc />
        IEnumerator<KeyValuePair<string, JToken>> IEnumerable<KeyValuePair<string, JToken>>.GetEnumerator()
        {
            return myDict.GetEnumerator();
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable) myDict).GetEnumerator();
        }

        /// <inheritdoc />
        void ICollection<KeyValuePair<string, JToken>>.Add(KeyValuePair<string, JToken> item)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        void ICollection<KeyValuePair<string, JToken>>.Clear()
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        bool ICollection<KeyValuePair<string, JToken>>.Contains(KeyValuePair<string, JToken> item)
        {
            return myDict.Contains(item);
        }

        /// <inheritdoc />
        void ICollection<KeyValuePair<string, JToken>>.CopyTo(KeyValuePair<string, JToken>[] array, int arrayIndex)
        {
            myDict.CopyTo(array, arrayIndex);
        }

        /// <inheritdoc />
        bool ICollection<KeyValuePair<string, JToken>>.Remove(KeyValuePair<string, JToken> item)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        bool ICollection<KeyValuePair<string, JToken>>.IsReadOnly => _IsReadOnly;

        /// <inheritdoc />
        void IDictionary<string, JToken>.Add(string key, JToken value)
        {
            if (_IsReadOnly) throw new NotSupportedException();
            myDict.Add(key, value);
        }

        #endregion
    }
}
