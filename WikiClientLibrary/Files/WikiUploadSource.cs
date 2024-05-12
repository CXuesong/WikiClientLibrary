using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Files;

/// <summary>
/// Base class for content that can be used for file-uploading.
/// </summary>
/// <seealso cref="WikiSiteExtensions.UploadAsync(WikiSite,string,WikiUploadSource,string,bool)"/>
public abstract class WikiUploadSource
{
    /// <summary>
    /// Gets the additional fields that will override the default <c>action=upload</c> parameters.
    /// </summary>
    public abstract IEnumerable<KeyValuePair<string, object>> GetUploadParameters(SiteInfo siteInfo);
}

/// <summary>
/// Represents uploadable content contained in a <see cref="Stream"/>.
/// </summary>
public class StreamUploadSource : WikiUploadSource
{

    /// <param name="stream">Stream content of the file to be uploaded.</param>
    public StreamUploadSource(Stream stream)
    {
            Stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

    /// <summary>Stream content of the file to be uploaded.</summary>
    public Stream Stream { get; }
        
    /// <inheritdoc />
    public override IEnumerable<KeyValuePair<string, object>> GetUploadParameters(SiteInfo siteInfo)
    {
            return new[] {new KeyValuePair<string, object>("file", Stream)};
        }
    /// <inheritdoc />
    public override string ToString()
    {
            return "StreamUploadSource(" + Stream + ")";
        }
}

/// <summary>
/// Uploadable content identified by <c>filekey</c> in MW upload API.
/// </summary>
public class FileKeyUploadSource : WikiUploadSource
{

    /// <param name="fileKey">File key (or session key before MW1.17) of the previously stashed result.</param>
    public FileKeyUploadSource(string fileKey)
    {
            FileKey = fileKey;
        }

    /// <summary>File key (or session key before MW1.17) of the previously stashed result.</summary>
    public virtual string FileKey { get; }

    /// <inheritdoc />
    public override IEnumerable<KeyValuePair<string, object>> GetUploadParameters(SiteInfo siteInfo)
    {
            if (siteInfo == null) throw new ArgumentNullException(nameof(siteInfo));
            if (FileKey == null) throw new ArgumentNullException(nameof(FileKey));
            return new[] { new KeyValuePair<string, object>(siteInfo.Version.Above(1, 18) ? "filekey" : "sessionkey", FileKey) };
        }

    /// <inheritdoc />
    public override string ToString()
    {
            return "FileKeyUploadSource(" + FileKey + ")";
        }
}

/// <summary>
/// Uploadable content identified by external file URL.
/// </summary>
/// <remarks>
/// Note that not all the Mediawiki sites allow uploading by external file URL.
/// Especially, Wikimedia and Wikia sites does not allow this.
/// </remarks>
public class ExternalFileUploadSource : WikiUploadSource
{
    /// <param name="sourceUrl">The URL of the file to be uploaded.</param>
    public ExternalFileUploadSource(string sourceUrl)
    {
            SourceUrl = sourceUrl;
        }

    /// <summary>The URL of the file to be uploaded.</summary>
    public virtual string SourceUrl { get; }

    /// <inheritdoc />
    public override IEnumerable<KeyValuePair<string, object>> GetUploadParameters(SiteInfo siteInfo)
    {
            if (SourceUrl == null) throw new ArgumentNullException(nameof(SourceUrl));
            return new[] {new KeyValuePair<string, object>("url", SourceUrl)};
        }

    /// <inheritdoc />
    public override string ToString()
    {
            return "ExternalFileUploadSource(" + SourceUrl + ")";
        }
}