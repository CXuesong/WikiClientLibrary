using System.Collections.Immutable;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Pages.Queries.Properties;

namespace WikiClientLibrary.Files;

/// <summary>
/// Represents a revision of a file or image.
/// </summary>
/// <remarks>
/// For revision of a MediaWiki page, see <see cref="Revision"/>.
/// </remarks>
/// <seealso cref="FileInfoPropertyGroup"/>
/// <seealso cref="FileInfoPropertyProvider"/>
[JsonObject(MemberSerialization.OptIn)]
public class FileRevision
{

    /// <summary>
    /// Gets the stub of page this revision applies to.
    /// </summary>
    public WikiPageStub Page { get; internal set; }

    /// <summary>
    /// Formatted metadata combined from multiple sources. Results are HTML formatted. (MW 1.17+)
    /// </summary>
    /// <seealso cref="FileInfoPropertyProvider.QueryExtMetadata"/>
    [JsonProperty]
    public IReadOnlyDictionary<string, FileRevisionExtMetadataValue> ExtMetadata { get; private set; }
        = ImmutableDictionary<string, FileRevisionExtMetadataValue>.Empty;

    /// <summary>
    /// Whether the file is anonymous. (MW ~1.33-)
    /// </summary>
    /// <remarks>
    /// Before ~MW 1.33, if this revision indicates a stashed file, this property will be <c>true</c>.
    /// </remarks>
    [JsonProperty("anon")]
    public bool IsAnonymous { get; private set; }

    [JsonProperty]
    public int BitDepth { get; private set; }

    /// <summary>
    /// MIME type of the file.
    /// </summary>
    [JsonProperty]
    public string Mime { get; private set; } = "";

    /// <summary>
    /// The time and date of the revision.
    /// </summary>
    [JsonProperty]
    public DateTime TimeStamp { get; private set; }

    /// <summary>
    /// Name of the user uploading this file revision.
    /// </summary>
    [JsonProperty("user")]
    public string UserName { get; private set; } = "";

    /// <summary>
    /// The comment associated with the upload of this revision.
    /// </summary>
    [JsonProperty]
    public string Comment { get; private set; } = "";

    /// <summary>
    /// Url of the file.
    /// </summary>
    [JsonProperty]
    public string Url { get; private set; } = "";

    /// <summary>
    /// Url of the description page.
    /// </summary>
    [JsonProperty]
    public string DescriptionUrl { get; private set; } = "";

    /// <summary>
    /// Size of the file. In bytes.
    /// </summary>
    [JsonProperty]
    public int Size { get; private set; }

    [JsonProperty]
    public int Width { get; private set; }

    [JsonProperty]
    public int Height { get; private set; }

    /// <summary>
    /// The file's SHA-1 hash.
    /// </summary>
    [JsonProperty]
    public string Sha1 { get; private set; } = "";
}

/// <summary>
/// Represents the value and source of an entry of <seealso cref="FileRevision"/> extmetadata.
/// </summary>
/// <seealso cref="FileRevision.ExtMetadata"/>
[JsonObject(MemberSerialization.OptIn)]
public class FileRevisionExtMetadataValue
{

    /// <summary>Metadata value.</summary>
    /// <remarks>
    /// According to <a href="https://www.mediawiki.org/wiki/API:Imageinfo">mw:API:Imageinfo</a>,
    /// the metadata value is expected to be formatted HTML expression.
    /// But sometimes the value could be <c>"True"</c>, <c>"true"</c>, or JSON numeric expression.
    /// You need to cast the returned value into your expected CLR type before working on it.
    /// </remarks>
    [JsonProperty]
    public JToken? Value { get; private set; }

    /// <summary>Source of the metadata value.</summary>
    /// <remarks>See <see cref="FileRevisionExtMetadataValueSources"/> for a list of possible metadata sources.</remarks>
    [JsonProperty]
    public string Source { get; private set; } = "";

    // https://github.com/wikimedia/mediawiki/blob/a638c0dce0b5a71c3c42ddf7e38e11e7bcd61f7a/includes/media/FormatMetadata.php#L1712
    /// <summary>Whether this metadata field is hidden on File page by default.</summary>
    [JsonProperty]
    public bool Hidden { get; private set; }

    /// <inheritdoc />
    public override string ToString() => $"{Value} ({Source})";
}

/// <summary>
/// Contains non-exhaustive possible values of <c>extmetadata</c> value sources.
/// </summary>
public static class FileRevisionExtMetadataValueSources
{

    // https://github.com/wikimedia/mediawiki/blob/a638c0dce0b5a71c3c42ddf7e38e11e7bcd61f7a/includes/media/FormatMetadata.php#L1798
    /// <summary>Provided by MediaWiki metadata.</summary>
    public const string MediaWikiMetadata = "mediawiki-metadata";

    // https://github.com/wikimedia/mediawiki-extensions-CommonsMetadata/blob/21a72a786641755da3bbe0f212f66bfc34a1a07d/src/DataCollector.php#L182
    /// <summary>Provided by Wikimedia Commons category.</summary>
    public const string CommonsCategories = "commons-categories";

    // https://github.com/wikimedia/mediawiki-extensions-CommonsMetadata/blob/21a72a786641755da3bbe0f212f66bfc34a1a07d/src/DataCollector.php#L229
    /// <summary>Provided by Wikimedia Commons file description page.</summary>
    public const string CommonsDescriptionPage = "commons-desc-page";

    // https://github.com/wikimedia/mediawiki-extensions-CommonsMetadata/blob/21a72a786641755da3bbe0f212f66bfc34a1a07d/src/DataCollector.php#L240
    /// <summary>Provided by template usage on Wikimedia Commons file description page.</summary>
    public const string CommonsTemplates = "commons-templates";

    // Not sure for now.
    public const string Extension = "extension";

}