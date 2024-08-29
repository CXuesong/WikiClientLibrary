using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Flow;

internal static class FlowUtility
{

    public static readonly JsonSerializerOptions FlowJsonSerializer =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // Flow uses camel-case.
            Converters =
            {
                new WikiStringEnumJsonConverter(), new FlowUserStubConverter(),
            },
        };

    public static UserStub UserFromJson(JsonObject user)
    {
        if (user == null) throw new ArgumentNullException(nameof(user));
        Gender gender = (string)user["gender"] switch
        {
            "male" => Gender.Male,
            "female" => Gender.Female,
            _ => Gender.Unknown,
        };
        return new UserStub((string)user["name"], (int?)user["id"] ?? 0, gender, (string)user["wiki"]);
    }

    public static DateTime DateFromJavaScriptTicks(long ticks)
    {
        // 621355968000000000: 1970-01-01
        return new DateTime(621355968000000000L + ticks * 10000L, DateTimeKind.Utc);
    }

    public static IList<KeyValuePair<string, string>> ParseUrlQueryParametrs(string url)
    {
        if (url == null) throw new ArgumentNullException(nameof(url));
        var queryStarts = url.IndexOf('?');
        if (queryStarts < 0) return Array.Empty<KeyValuePair<string, string>>();
        var query = url[(queryStarts + 1)..];
        if (query.Length == 0) return Array.Empty<KeyValuePair<string, string>>();
        return query.Split('&').Select(p =>
        {
            var equalIndex = p.IndexOf('=');
            if (equalIndex < 0) return new KeyValuePair<string, string>(null, p);
            return new KeyValuePair<string, string>(p.Substring(0, equalIndex), p[(equalIndex + 1)..]);
        }).ToList();
    }

}

internal class FlowUserStubConverter : System.Text.Json.Serialization.JsonConverter<UserStub>
{

    /// <inheritdoc />
    public override UserStub Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return default;
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("Expect JSON object.");
        var jUser = JsonNode.Parse(ref reader, new JsonNodeOptions { PropertyNameCaseInsensitive = options.PropertyNameCaseInsensitive });
        if (jUser?["name"] == null)
        {
            Debug.Assert(jUser?["id"] == null);
            // jUser["gender"] == "unknown"
            Debug.Assert(jUser?["site"] == null);
            return default;
        }
        return FlowUtility.UserFromJson(jUser.AsObject());
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, UserStub value, JsonSerializerOptions options)
    {
        throw new NotSupportedException();
    }

}
