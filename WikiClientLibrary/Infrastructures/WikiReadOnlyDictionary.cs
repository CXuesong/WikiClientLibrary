using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WikiClientLibrary.Infrastructures;

/// <summary>
/// A dictionary with predefined strong-typed derived properties customizable by implementers.
/// </summary>
[JsonContract]
public class WikiReadOnlyDictionary : IDictionary<string, JsonElement>, IJsonOnDeserialized
{

    [JsonExtensionData] private readonly Dictionary<string, JsonElement> myDict = new();

    /// <summary>
    /// Gets the count of all properties.
    /// </summary>
    public int Count => myDict.Count;

    /// <summary>
    /// Tries to get the value of the specified property.
    /// </summary>
    /// <param name="key">The property name.</param>
    /// <returns>A clone of property value, OR <c>null</c> if such property cannot be found.</returns>
    public JsonElement this[string key]
    {
        get
        {
            var value = myDict[key];
            return value;
        }
        set
        {
            throw new NotSupportedException(Prompts.ExceptionCollectionReadOnly);
        }
    }

    /// <inheritdoc cref="GetInt64Value(string)"/>
    /// <summary>
    /// Gets the <see cref="int"/> value by property name.
    /// This overload raises exception for missing key.
    /// </summary>
    /// <see cref="GetInt64Value(string,long)"/>
    public int GetInt32Value(string key)
    {
        return myDict[key].GetInt32();
    }

    /// <inheritdoc cref="GetInt64Value(string,long)"/>
    /// <summary>
    /// Gets the <see cref="int"/> value by property name.
    /// A default value can be provided in case the specified key does not exist.
    /// </summary>
    public int GetInt32Value(string key, int defaultValue)
    {
        if (myDict.TryGetValue(key, out var value)) return value.GetInt32();
        return defaultValue;
    }

    /// <summary>
    /// Gets the <see cref="long"/> value by property name.
    /// This overload raises exception for missing key.
    /// </summary>
    /// <param name="key">The property name.</param>
    /// <returns>The converted value.</returns>
    /// <exception cref="KeyNotFoundException">The property is not found.</exception>
    /// <see cref="GetInt32Value(string,int)"/>
    public long GetInt64Value(string key)
    {
        return myDict[key].GetInt64();
    }

    /// <summary>
    /// Gets the <see cref="long"/> value by property name.
    /// A default value can be provided in case the specified key does not exist.
    /// </summary>
    /// <param name="key">The property name.</param>
    /// <param name="defaultValue">The default value.</param>
    /// <returns>The converted value - or - <paramref name="defaultValue"/>.</returns>
    public long GetInt64Value(string key, long defaultValue)
    {
        if (myDict.TryGetValue(key, out var value)) return value.GetInt64();
        return defaultValue;
    }

    /// <summary>
    /// Gets the <see cref="bool"/> value by property name.
    /// </summary>
    /// <param name="key">The property name.</param>
    /// <remarks>
    /// This method returns true if the specified key exists,
    /// and its value is something other than <c>null</c> (typically <c>""</c>).
    /// </remarks>
    public bool GetBooleanValue(string key)
    {
        return myDict.TryGetValue(key, out var value) && value.ValueKind != JsonValueKind.Null;
    }

    /// <summary>
    /// Gets the <see cref="string"/> value by property name.
    /// </summary>
    /// <param name="key">The property name.</param>
    /// <returns>The converted value - or - <c>null</c> if the specified key does not exist.</returns>
    public string? GetStringValue(string key)
    {
        if (myDict.TryGetValue(key, out var value)) return value.GetString();
        return null;
    }

    /// <inheritdoc />
    public ICollection<string> Keys => myDict.Keys;

    /// <inheritdoc />
    bool IDictionary<string, JsonElement>.Remove(string key)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public bool ContainsKey(string key)
    {
        return myDict.ContainsKey(key);
    }

    /// <inheritdoc />
    public bool TryGetValue(string key, out JsonElement value)
    {
        return myDict.TryGetValue(key, out value);
    }

    /// <inheritdoc />
    public ICollection<JsonElement> Values => myDict.Values;

#region Explicit Interface Implementations

    /// <inheritdoc />
    IEnumerator<KeyValuePair<string, JsonElement>> IEnumerable<KeyValuePair<string, JsonElement>>.GetEnumerator()
    {
        return myDict.GetEnumerator();
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)myDict).GetEnumerator();
    }

    /// <inheritdoc />
    void ICollection<KeyValuePair<string, JsonElement>>.Add(KeyValuePair<string, JsonElement> item)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    void ICollection<KeyValuePair<string, JsonElement>>.Clear()
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    bool ICollection<KeyValuePair<string, JsonElement>>.Contains(KeyValuePair<string, JsonElement> item)
    {
        return myDict.Contains(item);
    }

    /// <inheritdoc />
    void ICollection<KeyValuePair<string, JsonElement>>.CopyTo(KeyValuePair<string, JsonElement>[] array, int arrayIndex)
    {
        ((ICollection<KeyValuePair<string, JsonElement>>)myDict).CopyTo(array, arrayIndex);
    }

    /// <inheritdoc />
    bool ICollection<KeyValuePair<string, JsonElement>>.Remove(KeyValuePair<string, JsonElement> item)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    bool ICollection<KeyValuePair<string, JsonElement>>.IsReadOnly => true;

    /// <inheritdoc />
    void IDictionary<string, JsonElement>.Add(string key, JsonElement value)
        => throw new NotSupportedException();

#endregion

    protected virtual void OnDeserialized()
    {
    }

    /// <inheritdoc />
    void IJsonOnDeserialized.OnDeserialized() => OnDeserialized();

}
