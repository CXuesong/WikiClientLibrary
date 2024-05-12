using System.Globalization;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace WikiClientLibrary.Infrastructures;

/// <summary>
/// Handles MediaWiki boolean values.
/// </summary>
/// <remarks>
/// See https://www.mediawiki.org/wiki/API:Data_formats#Boolean_values .
/// Aside from this convention, the converter can also recognize string values such as "false" or "False".
/// Note this convention is used in Flow extension.
/// </remarks>
public class WikiBooleanJsonConverter : JsonConverter
{

    /// <summary>
    /// Writes the JSON representation of the object.
    /// </summary>
    /// <param name="writer">The <see cref="T:Newtonsoft.Json.JsonWriter"/> to write to.</param><param name="value">The value.</param><param name="serializer">The calling serializer.</param>
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if ((bool)value) writer.WriteValue("");
    }

    /// <summary>
    /// Reads the JSON representation of the object.
    /// </summary>
    /// <param name="reader">The <see cref="T:Newtonsoft.Json.JsonReader"/> to read from.</param><param name="objectType">Type of the object.</param><param name="existingValue">The existing value of object being read.</param><param name="serializer">The calling serializer.</param>
    /// <returns>
    /// The object value.
    /// </returns>
    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var str = existingValue as string;
        return existingValue != null && !string.Equals(str, "false", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether this instance can convert the specified object type.
    /// </summary>
    /// <param name="objectType">Type of the object.</param>
    /// <returns>
    /// <c>true</c> if this instance can convert the specified object type; otherwise, <c>false</c>.
    /// </returns>
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(bool);
    }

}

/// <summary>
/// Converts all-lower-case enum name in MediaWiki API response to the enum value.
/// </summary>
public class WikiStringEnumJsonConverter : JsonConverter
{

    /// <inheritdoc />
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var ev = (Enum)value;
        writer.WriteValue(ev.ToString("G").ToLowerInvariant());
    }

    /// <inheritdoc />
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType != JsonToken.String) throw new JsonException("Expect enum string.");
        return Enum.Parse(objectType, (string)reader.Value, true);
    }

    /// <inheritdoc />
    public override bool CanConvert(Type objectType)
    {
        return objectType.IsEnum;
    }

}

/// <summary>
/// Handles the conversion between MediaWiki API timestamp and its CLR counterparts:
/// <see cref="DateTime"/> and <see cref="DateTimeOffset"/>.
/// </summary>
/// <remarks>
/// <para>This converter uses ISO 8601 format to serialize the timestamp,
/// and uses <c>"infinity"</c> for <seealso cref="DateTime.MaxValue"/>
/// and <seealso cref="DateTimeOffset.MaxValue"/>.</para>
/// <para>This converter uses <seealso cref="MediaWikiHelper.ParseDateTime"/>
/// and <seealso cref="MediaWikiHelper.ParseDateTimeOffset"/> to parse the timestamp.
/// See their documentation respectively for more behavioral information.</para>
/// </remarks>
public class WikiDateTimeJsonConverter : JsonConverter
{

    /// <inheritdoc />
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        // This function is actually not used. We use Utility.ToWikiQueryValue.
        if (value is DateTimeOffset dto)
        {
            if (dto == DateTimeOffset.MaxValue)
                writer.WriteValue("infinity");
            else
                writer.WriteValue(dto.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture));
            return;
        }
        if (value is DateTime dt)
        {
            if (dt == DateTime.MaxValue)
                writer.WriteValue("infinity");
            else
                writer.WriteValue(dt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture));
            return;
        }
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.String)
        {
            var expr = (string)reader.Value;
            if (objectType == typeof(DateTimeOffset))
                return MediaWikiHelper.ParseDateTimeOffset(expr);
            if (objectType == typeof(DateTime))
                return MediaWikiHelper.ParseDateTime(expr);
        }
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(DateTime) || objectType == typeof(DateTimeOffset);
    }

}

/// <summary>
/// All-lower-case property naming strategy as used in MediaWiki API response.
/// </summary>
public class WikiJsonNamingStrategy : NamingStrategy
{

    /// <summary>
    /// Resolves the specified property name.
    /// </summary>
    /// <param name="name">The property name to resolve.</param>
    /// <returns>
    /// The resolved property name.
    /// </returns>
    protected override string ResolvePropertyName(string name)
    {
        return name.ToLowerInvariant();
    }

}

public class WikiJsonContractResolver : DefaultContractResolver
{

    /// <inheritdoc />
    protected override string ResolvePropertyName(string propertyName)
    {
        if (NamingStrategy != null) return NamingStrategy.GetPropertyName(propertyName, false);
        return propertyName.ToLowerInvariant();
    }

    /// <inheritdoc />
    protected override string ResolveDictionaryKey(string dictionaryKey)
    {
        if (NamingStrategy != null) return NamingStrategy.GetDictionaryKey(dictionaryKey);
        // We keep the case of dictionary keys intact.
        return dictionaryKey;
    }

    /// <inheritdoc />
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        var prop = base.CreateProperty(member, memberSerialization);
        // For boolean values, omit the default value at all. (no `"boolvalue": null` in this case)
        if (prop.PropertyType == typeof(bool))
        {
            prop.DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate;
        }
        return prop;
    }

}
