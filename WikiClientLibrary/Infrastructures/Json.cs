using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WikiClientLibrary.Infrastructures;

/// <summary>
/// Handles MediaWiki boolean values.
/// </summary>
/// <remarks>
/// See https://www.mediawiki.org/wiki/API:Data_formats#Boolean_values .
/// Aside from this convention, the converter can also recognize string values such as "false" or "False".
/// Note this convention is used in Flow extension.
/// </remarks>
public class WikiBooleanJsonConverter : JsonConverter<bool>
{

    /// <inheritdoc />
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString();
        return s != null && !s.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
    {
        if (value) writer.WriteStringValue("");
        else writer.WriteNullValue();
    }

}

/// <summary>
/// Converts all-lower-case enum name in MediaWiki API response to the enum value.
/// </summary>
public class WikiStringEnumJsonConverter : JsonConverterFactory
{

    private static readonly ConcurrentDictionary<Type, JsonConverter> enumConverterInst = new();

    /// <inheritdoc />
    public override bool CanConvert(Type objectType)
    {
        return objectType.IsEnum;
    }

    /// <inheritdoc />
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return enumConverterInst.GetOrAdd(typeToConvert, t =>
            (JsonConverter)Activator.CreateInstance(typeof(EnumJsonConverterImpl<>).MakeGenericType(typeToConvert))!
        );
    }

    private sealed class EnumJsonConverterImpl<T> : JsonConverter<T> where T : struct, Enum
    {

        /// <inheritdoc />
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var expr = reader.GetString();
            // https://github.com/dotnet/runtime/issues/28482
            if (expr == null) throw new JsonException($"Expect {typeof(T)} enum value. Received: null.");
            return Enum.Parse<T>(expr, true);
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("G").ToLowerInvariant());
        }

    }

}

/// <summary>
/// Handles the conversion between MediaWiki API timestamp and its CLR counterparts.
/// This specific class handles the timestamp represented by <see cref="DateTimeOffset"/>.
/// </summary>
/// <remarks>
/// <para>This converter uses ISO 8601 format to serialize the timestamp,
/// and uses <c>"infinity"</c> for <seealso cref="DateTime.MaxValue"/>
/// and <seealso cref="DateTimeOffset.MaxValue"/>.</para>
/// <para>This converter uses <seealso cref="MediaWikiHelper.ParseDateTime"/>
/// and <seealso cref="MediaWikiHelper.ParseDateTimeOffset"/> to parse the timestamp.
/// See their documentation respectively for more behavioral information.</para>
/// </remarks>
/// <seealso cref="WikiDateTimeConverter"/>
public class WikiDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{

    /// <inheritdoc />
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var expr = reader.GetString();
        if (expr == null) throw new JsonException("Expect a timestamp expression. Received: null.");
        return MediaWikiHelper.ParseDateTimeOffset(expr);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        if (value == DateTimeOffset.MaxValue)
            writer.WriteStringValue("infinity");
        else
            writer.WriteStringValue(value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture));
    }

}

/// <summary>
/// Handles the conversion between MediaWiki API timestamp and its CLR counterparts.
/// This specific class handles the timestamp represented by <see cref="DateTimeOffset"/>.
/// </summary>
/// <remarks>
/// See the "Remarks" section of <see cref="WikiDateTimeOffsetConverter"/> for the conversion behavior.
/// </remarks>
/// <seealso cref="WikiDateTimeOffsetConverter"/>
public class WikiDateTimeConverter : JsonConverter<DateTime>
{

    /// <inheritdoc />
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var expr = reader.GetString();
        if (expr == null) throw new JsonException("Expect a timestamp expression. Received: null.");
        return MediaWikiHelper.ParseDateTime(expr);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        if (value == DateTime.MaxValue)
            writer.WriteStringValue("infinity");
        else
            writer.WriteStringValue(value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture));
    }

}

/// <summary>
/// Handles the serialization and deserialization of instances derived from <see cref="WikiReadOnlyDictionary"/>.
/// </summary>
public sealed class WikiReadOnlyDictionaryConverterFactory : JsonConverterFactory
{

    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
    {
        return typeof(WikiReadOnlyDictionary).IsAssignableFrom(typeToConvert)
               && typeToConvert is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false };
    }

    /// <inheritdoc />
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return (JsonConverter?)typeof(WikiReadOnlyDictionary.DictionaryJsonConverter<>)
            .MakeGenericType(typeToConvert)
            .InvokeMember(nameof(WikiReadOnlyDictionary.DictionaryJsonConverter<WikiReadOnlyDictionary>.Default),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.GetField, null, null, null);
    }

}

/// <summary>
/// All-lower-case property naming strategy as used in MediaWiki API response.
/// </summary>
public class WikiJsonNamingPolicy : JsonNamingPolicy
{

    /// <inheritdoc />
    public override string ConvertName(string name)
    {
        return name.ToLowerInvariant();
    }

}
