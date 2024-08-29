using System.Text.Json.Serialization;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Wikia.WikiaApi;

[JsonContract]
public sealed class RelatedPageItem
{

    /// <summary>Absolute URL of the page.</summary>
    [JsonInclude]
    [JsonPropertyName("url")]
    public string Url { get; private set; }

    /// <summary>Full title of the page.</summary>
    [JsonPropertyName("title")]
    public string Title { get; init; }

    /// <summary>ID of the page.</summary>
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonInclude]
    [JsonPropertyName("imgUrl")]
    public string ImageUrl { get; private set; }

    [JsonPropertyName("imgOriginalDimensions")]
    public string ImageOriginalDimensions { get; init; }

    /// <summary>Excerpt of the page.</summary>
    [JsonPropertyName("text")]
    public string Text { get; init; }

    internal void ApplyBasePath(string basePath)
    {
        if (Url != null) Url = MediaWikiHelper.MakeAbsoluteUrl(basePath, Url);
        if (ImageUrl != null) ImageUrl = MediaWikiHelper.MakeAbsoluteUrl(basePath, ImageUrl);
    }

    /// <inheritdoc />
    public override string ToString() => $"[{Id}]{Title}";

}
