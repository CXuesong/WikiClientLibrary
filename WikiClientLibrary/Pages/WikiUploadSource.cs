using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Pages
{
    /// <summary>
    /// Base class for content that can be used with <see cref="FilePage.UploadAsync"/>.
    /// </summary>
    public abstract class WikiUploadSource
    {
        /// <summary>
        /// Gets the additional fields that will override the default action=upload parameters.
        /// </summary>
        public abstract IEnumerable<KeyValuePair<string, object>> GetUploadParameters(SiteInfo siteInfo);
    }

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

    }

    public class FileKeyUploadSource : WikiUploadSource
    {

        private static readonly Version v118 = new Version(1, 18);

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
            return new[]
            {
                new KeyValuePair<string, object>(
                    siteInfo.Version >= v118 ? "filekey" : "sessionkey", FileKey)
            };
        }

    }

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
    }

}
