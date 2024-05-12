using Newtonsoft.Json;

namespace WikiClientLibrary.Wikia.WikiaApi;

/// <summary>
/// Represents an item in the Wikia local wiki site search result.
/// </summary>
[JsonObject(MemberSerialization.OptIn)]
public class LocalWikiSearchResultItem
{

    /// <summary>
    /// Id of the page.
    /// </summary>
    [JsonProperty("id")]
    public int Id { get; private set; }

    /// <summary>
    /// Gets the full title of the page.
    /// </summary>
    [JsonProperty("title")]
    public string Title { get; private set; }

    /// <summary>
    /// Absolute URL of the page.
    /// </summary>
    [JsonProperty]
    public string Url { get; private set; }

    /// <summary>
    /// Namespace id of the page.
    /// </summary>
    [JsonProperty("ns")]
    public int NamespaceId { get; private set; }

    /// <summary>
    /// Quality of matching.
    /// </summary>
    [JsonProperty]
    public int Quality { get; private set; }

    /// <summary>
    /// Gets the parsed HTML snippet of the page.
    /// </summary>  
    [JsonProperty]
    public string Snippet { get; private set; }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"[{Id}]{Title}";
    }

}
