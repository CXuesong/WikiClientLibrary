using System.Text.Json.Nodes;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Wikibase;

/// <summary>
/// Tabular data allows users to create CSV-like tables of data,
/// and use them from other wikis to create automatic tables, lists, and graphs.
/// </summary>
/// <remarks>
/// See "https://www.mediawiki.org/wiki/Help:Tabular_Data" for more documentation about tabular data.
/// </remarks>
//[JsonObject(MemberSerialization.OptIn, NamingStrategyType = typeof(CamelCaseNamingStrategy))]
[JsonContract]
internal class WikibaseTabularData // Reserved for future use.
{

    public string? License { get; set; }

    public IDictionary<string, string>? Description { get; set; }

    public string? Sources { get; set; }

    public JsonObject? Schema { get; set; }

    public JsonArray? Data { get; set; }

}
