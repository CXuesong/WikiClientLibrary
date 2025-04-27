using System.Text.Json.Nodes;
using WikiClientLibrary.Files;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Pages.Queries.Properties;

/// <summary>
/// Provides information for MediaWiki files.
/// (<a href="https://www.mediawiki.org/wiki/API:Fileinfo">mw:API:Fileinfo</a>, MediaWiki 1.13+)
/// </summary>
public class FileInfoPropertyProvider : WikiPagePropertyProvider<FileInfoPropertyGroup>
{

    /// <summary>
    /// Whether lists formatted metadata combined from multiple sources. (MW 1.17+)
    /// </summary>
    /// <seealso cref="FileRevision.ExtMetadata"/>
    public bool QueryExtMetadata { get; set; }

    /// <inheritdoc />
    public override IEnumerable<KeyValuePair<string, object?>> EnumParameters(MediaWikiVersion version)
    {
        var properties = new List<string>
        {
            "timestamp",
            "user",
            "comment",
            "url",
            "size",
            "sha1",
            "mime",
        };
        if (QueryExtMetadata)
            properties.Add("extmetadata");

        return new OrderedKeyValuePairs<string, object?> { { "iiprop", string.Join("|", properties) } };
    }

    /// <inheritdoc />
    public override FileInfoPropertyGroup? ParsePropertyGroup(JsonObject json)
    {
        return FileInfoPropertyGroup.Create(json);
    }

    /// <inheritdoc />
    public override string? PropertyName => "imageinfo";

}

/// <summary>
/// Contains properties for MediaWiki files.
/// (<a href="https://www.mediawiki.org/wiki/API:Fileinfo">mw:API:Fileinfo</a>,
/// <a href="https://www.mediawiki.org/wiki/API:Imageinfo">mw:API:Imageinfo</a>,
/// MediaWiki 1.13+)
/// </summary>
public class FileInfoPropertyGroup : WikiPagePropertyGroup
{

    private static readonly FileInfoPropertyGroup Empty = new FileInfoPropertyGroup();

    private readonly FileRevision[] _Revisions;

    internal static FileInfoPropertyGroup? Create(JsonObject jpage)
    {
        var info = jpage["imageinfo"]?.AsArray();
        // jpage["imageinfo"] == null indicates the page may not be a valid File.
        if (info == null) return null;
        if (info.Count == 0) return Empty;
        var stub = MediaWikiHelper.PageStubFromJson(jpage);
        return new FileInfoPropertyGroup(stub, info);
    }

    private FileInfoPropertyGroup()
    {
        _Revisions = Array.Empty<FileRevision>();
        Revisions = Array.AsReadOnly(_Revisions);
    }

    private FileInfoPropertyGroup(WikiPageStub page, JsonArray jrevisions)
    {
        _Revisions = jrevisions
            .Select(jr => MediaWikiHelper.FileRevisionFromJson(jr!.AsObject(), page))
            .ToArray();
        Revisions = Array.AsReadOnly(_Revisions);
    }

    public IReadOnlyCollection<FileRevision> Revisions { get; }

    /// <summary>
    /// Gets the latest file revision information.
    /// </summary>
    public FileRevision? LatestRevision
    {
        get
        {
            var revs = _Revisions;
            if (revs.Length == 0) return null;
            // Take sort order into consideration.
            return revs[0].TimeStamp >= revs[^1].TimeStamp ? revs[0] : revs[^1];
        }
    }

}
