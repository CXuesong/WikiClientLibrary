using System;
using System.Globalization;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace WikiClientLibrary.Infrastructures
{
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
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
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
            return objectType.GetTypeInfo().IsEnum;
        }
    }

    /// <summary>
    /// Handles the conversion between MediaWiki API timestamp and its CLR counterparts:
    /// <see cref="DateTime"/> and <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <remarks>
    /// <para>This converter handles `"infinity"` as <see cref="DateTime.MaxValue"/> or <see cref="DateTimeOffset.MaxValue"/>.</para>
    /// <para>For now this class only supports conversion of ISO 8601 format. If you are using this class
    /// and need more support within the API specification linked below, please open an issue in WCL
    /// repository.</para>
    /// <para>See <a href="https://www.mediawiki.org/wiki/API:Data_formats#Timestamps">mw:API:Data formats#Timestamps</a>
    /// for more information.</para>
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
                {
                    if (string.Equals(expr, "infinity", StringComparison.OrdinalIgnoreCase))
                        return DateTimeOffset.MaxValue;
                    // quote Timestamps are always output in ISO 8601 format. endquote
                    if (DateTimeOffset.TryParseExact(expr, "yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out var result))
                        return result;
                    // backup plan
                    return DateTimeOffset.Parse(expr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                }
                if (objectType == typeof(DateTime))
                {
                    if (string.Equals(expr, "infinity", StringComparison.OrdinalIgnoreCase))
                        return DateTime.MaxValue;
                    if (DateTime.TryParseExact(expr, "yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out var result))
                        return result;
                    return DateTime.Parse(expr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                }
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

}