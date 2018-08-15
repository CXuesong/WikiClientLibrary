using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

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

        /// <summary>
        /// Gets the <see cref="int"/> value by property name.
        /// This overload raises exception for missing key.
        /// </summary>
        /// <param name="key">The property name.</param>
        /// <returns>The converted value.</returns>
        /// <exception cref="KeyNotFoundException">The property is not found.</exception>
        /// <see cref="GetInt32Value(string,int)"/>
        public int GetInt32Value(string key)
        {
            return (int)myDict[key];
        }

        /// <summary>
        /// Gets the <see cref="int"/> value by property name.
        /// A default value can be provided in case the specified key does not exist.
        /// </summary>
        /// <param name="key">The property name.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <returns>The converted value - or - <paramref name="defaultValue"/>.</returns>
        public int GetInt32Value(string key, int defaultValue)
        {
            if (myDict.TryGetValue(key, out var value)) return (int)value;
            return defaultValue;
        }

        /// <summary>
        /// Gets the <see cref="bool"/> value by property name.
        /// </summary>
        /// <param name="key">The property name.</param>
        /// <remarks>
        /// This method returns true if the specified key exists,
        /// and its value is something other than <c>null</c> (typically it's <c>""</c>).
        /// </remarks>
        public bool GetBooleanValue(string key)
        {
            return myDict.TryGetValue(key, out var value) && value.Type != JTokenType.Null;
        }

        /// <summary>
        /// Gets the <see cref="string"/> value by property name.
        /// </summary>
        /// <param name="key">The property name.</param>
        /// <returns>The converted value - or - <c>null</c> if the specified key does not exist.</returns>
        public string GetStringValue(string key)
        {
            if (myDict.TryGetValue(key, out var value)) return (string)value;
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
            return ((IEnumerable)myDict).GetEnumerator();
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
