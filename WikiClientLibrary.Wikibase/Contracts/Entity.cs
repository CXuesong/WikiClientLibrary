using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Wikibase.Contracts;

[JsonContract]
internal sealed class Entity
{

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; set; }

    public string? Type { get; set; }

    public string? DataType { get; set; }

    public string? Id { get; set; }

    public IDictionary<string, MonolingualText>? Labels { get; set; }

    public IDictionary<string, MonolingualText>? Descriptions { get; set; }

    public IDictionary<string, ICollection<MonolingualText>>? Aliases { get; set; }

    public IDictionary<string, ICollection<Claim>>? Claims { get; set; }

    public IDictionary<string, SiteLink>? Sitelinks { get; set; }

}

[JsonContract]
internal sealed class MonolingualText
{

    public required string Language { get; set; }

    // This field must exist, even if Remove == true.
    // Otherwise, there will be internal_api_error_TypeError
    public string Value { get; set; } = "";

    public bool Remove { get; set; }

}

[JsonContract]
internal sealed class Claim
{

    public Snak? MainSnak { get; set; }

    public string? Type { get; set; }

    public IDictionary<string, ICollection<Snak>>? Qualifiers { get; set; }

    [JsonPropertyName("qualifiers-order")]
    public IList<string>? QualifiersOrder { get; set; }

    public string? Id { get; set; }

    public string? Rank { get; set; }

    public IList<Reference>? References { get; set; }

    public bool Remove { get; set; }

}

[JsonContract]
internal sealed class Snak
{

    public string? SnakType { get; set; }

    public required string Property { get; set; }

    public string? Hash { get; set; }

    public JsonObject? DataValue { get; set; }

    public string? DataType { get; set; }

}

[JsonContract]
internal sealed class Reference
{

    public string? Hash { get; set; }

    public IDictionary<string, ICollection<Snak>>? Snaks { get; set; }

    [JsonPropertyName("snaks-order")]
    public IList<string>? SnaksOrder { get; set; }

}

[JsonContract]
internal sealed class SiteLink
{

    public required string Site { get; set; }

    public string? Title { get; set; }

    public IList<string>? Badges { get; set; }

    public bool Remove { get; set; }

}
