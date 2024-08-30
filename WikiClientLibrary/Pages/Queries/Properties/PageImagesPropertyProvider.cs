using System.Text.Json.Nodes;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Pages.Queries.Properties;

/// <summary>
/// Provides information about images on the page, such as thumbnail and presence of photos.
/// (<a href="https://www.mediawiki.org/wiki/Special:MyLanguage/Extension:PageImages#API">mw:Extension:PageImages#API</a>)
/// </summary>
public class PageImagesPropertyProvider : WikiPagePropertyProvider<PageImagesPropertyGroup>
{

    /// <summary>
    /// Gets/sets a value that determines whether to fetch for URL and dimensions of thumbnail image associated with page, if any.
    /// </summary>
    public bool QueryOriginalImage { get; set; }

    /// <summary>
    /// Gets/sets the maximum thumbnail dimension.
    /// </summary>
    /// <value>Maximum thumbnail dimension, in px; or <c>0</c> to disable thumbnail image fetching.</value>
    /// <remarks>The default value is 50.</remarks>
    public int ThumbnailSize { get; set; } = 50;

    /// <inheritdoc />
    public override int GetMaxPaginationSize(MediaWikiVersion version, bool apiHighLimits)
    {
        return apiHighLimits ? 100 : 50;
    }

    /// <inheritdoc />
    public override IEnumerable<KeyValuePair<string, object?>> EnumParameters(MediaWikiVersion version)
    {
        var p = new OrderedKeyValuePairs<string, object?>();
        if (QueryOriginalImage && ThumbnailSize > 0)
            p.Add("piprop", "original|thumbnail|name");
        else if (QueryOriginalImage)
            p.Add("piprop", "original|name");
        else if (ThumbnailSize > 0)
            p.Add("piprop", "thumbnail|name");
        else
            p.Add("piprop", "name");
        if (ThumbnailSize >= 0)
            p.Add("pithumbsize", ThumbnailSize);
        return p;
    }

    /// <inheritdoc />
    public override string? PropertyName => "pageimages";

    /// <inheritdoc />
    public override PageImagesPropertyGroup? ParsePropertyGroup(JsonObject json)
    {
        if (json == null) throw new ArgumentNullException(nameof(json));
        return PageImagesPropertyGroup.Create(json);
    }

}

/// <summary>
/// Contains information for page image URL along with image size.
/// </summary>
public sealed class PageImageInfo
{

    /// <summary>Image URL.</summary>
    public required string Url { get; init; }

    /// <summary>Image width, in pixel.</summary>
    public int Width { get; init; }

    /// <summary>Image height, in pixel.</summary>
    public int Height { get; init; }

    /// <inheritdoc />
    public override string ToString()
    {
        return Url;
    }

}

/// <summary>
/// Contains information about images on the page, such as thumbnail and presence of photos.
/// (<a href="https://www.mediawiki.org/wiki/Special:MyLanguage/Extension:PageImages#API">mw:Extension:PageImages#API</a>)
/// </summary>
public class PageImagesPropertyGroup : WikiPagePropertyGroup
{

    private static readonly PageImagesPropertyGroup Empty = new PageImagesPropertyGroup();

    internal static PageImagesPropertyGroup Create(JsonNode jpage)
    {
        if (jpage["original"] == null && jpage["thumbnail"] == null && jpage["pageimage"] == null)
            return Empty;
        return new PageImagesPropertyGroup(jpage);
    }

    private PageImagesPropertyGroup()
    {
        OriginalImage = null;
        ThumbnailImage = null;
        ImageTitle = "";
    }

    private PageImagesPropertyGroup(JsonNode jpage)
    {
        OriginalImage = jpage["original"] != null ? ParseImageInfo(jpage["original"]) : null;
        ThumbnailImage = jpage["thumbnail"] != null ? ParseImageInfo(jpage["thumbnail"]) : null;
        ImageTitle = (string?)jpage["pageimage"] ?? "";
    }

    private static PageImageInfo ParseImageInfo(JsonNode root)
    {
        return new PageImageInfo
        {
            Url = (string)root["source"],
            Width = (int)root["width"],
            Height = (int)root["height"],
        };
    }

    /// <summary>Gets the original image for the page image.</summary>
    public PageImageInfo? OriginalImage { get; }

    /// <summary>Gets the thumbnail for the page image.</summary>
    public PageImageInfo? ThumbnailImage { get; }

    /// <summary>Gets the file title for the page image.</summary>
    public string ImageTitle { get; }

}
