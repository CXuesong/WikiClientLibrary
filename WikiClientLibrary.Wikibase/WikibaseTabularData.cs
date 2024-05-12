using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace WikiClientLibrary.Wikibase;

/// <summary>
/// Tabular data allows users to create CSV-like tables of data,
/// and use them from other wikis to create automatic tables, lists, and graphs.
/// </summary>
/// <remarks>
/// See "https://www.mediawiki.org/wiki/Help:Tabular_Data" for more documentation about tabular data.
/// </remarks>
[JsonObject(MemberSerialization.OptIn, NamingStrategyType = typeof(CamelCaseNamingStrategy))]
internal class WikibaseTabularData // Reserved for future use.
{

    [JsonProperty]
    public string? License { get; set; }

    [JsonProperty]
    public IDictionary<string, string>? Description { get; set; }

    [JsonProperty]
    public string? Sources { get; set; }

    [JsonProperty]
    public JObject? Schema { get; set; }

    [JsonProperty]
    public JArray? Data { get; set; }

}
