using System.Text.Json.Serialization;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Wikia.WikiaApi;

/// <summary>
/// Represents an item in the Wikia local wiki site search result.
/// </summary>
[JsonContract]
public sealed class LocalWikiSearchResultItem
{

    /// <summary>
    /// Id of the page.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; init; }

    /// <summary>
    /// Gets the full title of the page.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; init; }

    /// <summary>
    /// Absolute URL of the page.
    /// </summary>
    public string Url { get; init; }

    /// <summary>
    /// Namespace id of the page.
    /// </summary>
    [JsonPropertyName("ns")]
    public int NamespaceId { get; init; }

    /// <summary>
    /// Quality of matching.
    /// </summary>
    public int Quality { get; init; }

    /// <summary>
    /// Gets the parsed HTML snippet of the page.
    /// </summary>  
    public string Snippet { get; init; }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"[{Id}]{Title}";
    }

}
