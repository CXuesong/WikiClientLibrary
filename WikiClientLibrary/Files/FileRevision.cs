using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using WikiClientLibrary.Infrastructures;
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
[JsonContract]
public sealed record FileRevision
{

    /// <summary>
    /// Gets the stub of page this revision applies to.
    /// </summary>
    public WikiPageStub Page { get; internal set; }

    /// <summary>
    /// Formatted metadata combined from multiple sources. Results are HTML formatted. (MW 1.17+)
    /// </summary>
    /// <seealso cref="FileInfoPropertyProvider.QueryExtMetadata"/>
    public IReadOnlyDictionary<string, FileRevisionExtMetadataValue> ExtMetadata { get; init; }
        = ImmutableDictionary<string, FileRevisionExtMetadataValue>.Empty;

    /// <summary>
    /// Whether the file is anonymous. (MW ~1.33-)
    /// </summary>
    /// <remarks>
    /// Before ~MW 1.33, if this revision indicates a stashed file, this property will be <c>true</c>.
    /// </remarks>
    [JsonPropertyName("anon")]
    public bool IsAnonymous { get; init; }

    public int BitDepth { get; init; }

    /// <summary>
    /// MIME type of the file.
    /// </summary>
    public string Mime { get; init; } = "";

    /// <summary>
    /// The time and date of the revision.
    /// </summary>
    public DateTime TimeStamp { get; init; }

    /// <summary>
    /// Name of the user uploading this file revision.
    /// </summary>
    [JsonPropertyName("user")]
    public string UserName { get; init; } = "";

    /// <summary>
    /// The comment associated with the upload of this revision.
    /// </summary>
    public string Comment { get; init; } = "";

    /// <summary>
    /// Url of the file.
    /// </summary>
    public string Url { get; init; } = "";

    /// <summary>
    /// Url of the description page.
    /// </summary>
    public string DescriptionUrl { get; init; } = "";

    /// <summary>
    /// Size of the file. In bytes.
    /// </summary>
    public int Size { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    /// <summary>
    /// The file's SHA-1 hash.
    /// </summary>
    public string Sha1 { get; init; } = "";

}

/// <summary>
/// Represents the value and source of an entry of <seealso cref="FileRevision"/> extmetadata.
/// </summary>
/// <seealso cref="FileRevision.ExtMetadata"/>
[JsonContract]
public sealed record FileRevisionExtMetadataValue
{

    /// <summary>Metadata value JSON.</summary>
    /// <remarks>
    /// According to <a href="https://www.mediawiki.org/wiki/API:Imageinfo">mw:API:Imageinfo</a>,
    /// the metadata value is expected to be formatted HTML expression.
    /// But sometimes the value could be a JSON <c>string</c> of <c>"True"</c>, <c>"true"</c>, or a JSON number.
    /// You need to retrieve the corresponding JSON value into your expected CLR type before working on it.
    /// Alternatively, you can leverage the <c>GetValueAs*</c> APIs in this class for more resilient type conversion.
    /// </remarks>
    public required JsonElement Value { get; init; }

    /// <summary>Source of the metadata value.</summary>
    /// <remarks>See <see cref="FileRevisionExtMetadataValueSources"/> for a list of possible metadata sources.</remarks>
    public string Source { get; init; } = "";

    // https://github.com/wikimedia/mediawiki/blob/a638c0dce0b5a71c3c42ddf7e38e11e7bcd61f7a/includes/media/FormatMetadata.php#L1712
    /// <summary>Whether this metadata field is hidden on File page by default.</summary>
    public bool Hidden { get; init; }

    /// <summary>
    /// Retrieves the metadata value, converted into the specified primitive type.
    /// </summary>
    public T? GetValueAs<T>() => WikiJsonElementHelper.ConvertTo<T>(Value);

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
