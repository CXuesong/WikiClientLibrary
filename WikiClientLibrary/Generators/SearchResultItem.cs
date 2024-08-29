using System.Text.Json.Serialization;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Generators;

/// <summary>
/// Represents an item in the search result.
/// </summary>
[JsonContract]
public sealed class SearchResultItem
{

    /// <summary>
    /// ID of the page.
    /// </summary>
    [JsonPropertyName("pageid")]
    public long Id { get; init; }

    /// <summary>
    /// Namespace id of the page.
    /// </summary>
    [JsonPropertyName("ns")]
    public int NamespaceId { get; init; }

    /// <summary>
    /// Gets the full title of the page.
    /// </summary>
    public string Title { get; init; } = "";

    /// <summary>
    /// Gets the content length, in bytes.
    /// </summary>
    [JsonPropertyName("size")]
    public int ContentLength { get; init; }

    /// <summary>
    /// Gets the word count.
    /// </summary>  
    public int WordCount { get; init; }

    /// <summary>
    /// Gets the parsed HTML snippet of the page.
    /// </summary>  
    public string Snippet { get; init; } = "";

    /// <summary>
    /// Gets the timestamp of when the page was last edited.
    /// </summary>  
    public DateTime TimeStamp { get; init; }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"[{Id}]{Title}";
    }

}
