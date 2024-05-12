using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WikiClientLibrary.Wikibase.Contracts;

[JsonObject(MemberSerialization.OptIn)]
internal class Entity
{

    [JsonExtensionData]
    public IDictionary<string, JToken>? ExtensionData { get; set; }

    [JsonProperty]
    public string? Type { get; set; }

    [JsonProperty]
    public string? DataType { get; set; }

    [JsonProperty]
    public string? Id { get; set; }

    [JsonProperty]
    public IDictionary<string, MonolingualText>? Labels { get; set; }

    [JsonProperty]
    public IDictionary<string, MonolingualText>? Descriptions { get; set; }

    [JsonProperty]
    public IDictionary<string, ICollection<MonolingualText>>? Aliases { get; set; }

    [JsonProperty]
    public IDictionary<string, ICollection<Claim>>? Claims { get; set; }

    [JsonProperty]
    public IDictionary<string, SiteLink>? Sitelinks { get; set; }

}

[JsonObject(MemberSerialization.OptIn)]
internal class MonolingualText
{

    [JsonProperty]
    public string Language { get; set; } = "";

    [JsonProperty]
    public string Value { get; set; } = "";

    [JsonProperty]
    public bool Remove { get; set; }

}

[JsonObject(MemberSerialization.OptIn)]
internal class Claim
{

    [JsonProperty]
    public Snak? MainSnak { get; set; }

    [JsonProperty]
    public string? Type { get; set; }

    [JsonProperty]
    public IDictionary<string, ICollection<Snak>>? Qualifiers { get; set; }

    [JsonProperty("qualifiers-order")]
    public IList<string>? QualifiersOrder { get; set; }

    [JsonProperty]
    public string? Id { get; set; }

    [JsonProperty]
    public string? Rank { get; set; }

    [JsonProperty]
    public IList<Reference>? References { get; set; }
        
    [JsonProperty]
    public bool Remove { get; set; }
}

[JsonObject(MemberSerialization.OptIn)]
internal class Snak
{
    [JsonProperty]
    public string? SnakType { get; set; }

    [JsonProperty]
    public string Property { get; set; } = "";

    [JsonProperty]
    public string? Hash { get; set; }

    [JsonProperty]
    public JObject? DataValue { get; set; }

    [JsonProperty]
    public string? DataType { get; set; }
}

[JsonObject(MemberSerialization.OptIn)]
internal class Reference
{
    [JsonProperty]
    public string? Hash { get; set; }

    [JsonProperty]
    public IDictionary<string, ICollection<Snak>>? Snaks { get; set; }

    [JsonProperty("snaks-order")]
    public IList<string>? SnaksOrder { get; set; }
}

[JsonObject(MemberSerialization.OptIn)]
internal class SiteLink
{
    [JsonProperty]
    public string Site { get; set; } = "";

    [JsonProperty]
    public string Title { get; set; } = "";

    [JsonProperty]
    public IList<string>? Badges { get; set; }

    [JsonProperty]
    public bool Remove { get; set; }
}