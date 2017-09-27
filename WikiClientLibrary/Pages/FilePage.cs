using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Pages
{
    /// <summary>
    /// Represents a file page on MediaWiki site.
    /// </summary>
    public class FilePage : WikiPage
    {
        // Use FilePage to distinguish from System.IO.File
        public FilePage(WikiSite site, string title) : base(site, title, BuiltInNamespaces.File)
        {
        }

        internal FilePage(WikiSite site) : base(site)
        {
        }

        /// <summary>
        /// Asynchronously uploads a file from an external URL.
        /// </summary>
        /// <param name="site">MedaiWiki site.</param>
        /// <param name="url">The URL of the file to be uploaded.</param>
        /// <param name="title">Title of the file, with or without File: prefix.</param>
        /// <param name="comment">Comment of the upload, as well as the page content if it doesn't exist.</param>
        /// <param name="ignoreWarnings">Ignore any warnings. This must be set to upload a new version of an existing image.</param>
        /// <exception cref="UploadException">
        /// There's warning from server, and <paramref name="ignoreWarnings"/> is <c>false</c>.
        /// Check <see cref="UploadException.UploadResult"/> for the warning message or continuing the upload.
        /// </exception>
        /// <exception cref="OperationFailedException"> There's an error while uploading the file. </exception>
        /// <exception cref="TimeoutException">
        /// Timeout specified in <see cref="WikiClientBase.Timeout"/> has been reached. Note in this
        /// invocation, there will be no retries.
        /// </exception>
        /// <returns>An <see cref="UploadResult"/>.</returns>
        /// <remarks>
        /// Upload from external source may take a while, so be sure to set a long <see cref="WikiClientBase.Timeout"/>
        /// in case the response from the server is delayed.
        /// </remarks>
        [Obsolete("Please use FilePage.UploadAsync instance methods instead.")]
        public static Task<UploadResult> UploadAsync(WikiSite site, string url, string title,
            string comment, bool ignoreWarnings)
        {
            return new FilePage(site, title).UploadFromAsync(url, comment, ignoreWarnings,
                AutoWatchBehavior.Default, CancellationToken.None);
        }

        /// <summary>
        /// Asynchronously uploads a file from an external URL.
        /// </summary>
        /// <param name="site">MedaiWiki site.</param>
        /// <param name="url">The URL of the file to be uploaded.</param>
        /// <param name="title">Title of the file, with or without File: prefix.</param>
        /// <param name="comment">Comment of the upload, as well as the page content if it doesn't exist.</param>
        /// <param name="ignoreWarnings">Ignore any warnings. This must be set to upload a new version of an existing image.</param>
        /// <param name="cancellationToken"></param>
        /// <exception cref="UploadException">
        /// There's warning from server, and <paramref name="ignoreWarnings"/> is <c>false</c>.
        /// Check <see cref="UploadException.UploadResult"/> for the warning message or continuing the upload.
        /// </exception>
        /// <exception cref="UnauthorizedAccessException">You do not have the permission to upload the file.</exception>
        /// <exception cref="OperationFailedException"> There's an error while uploading the file. </exception>
        /// <exception cref="TimeoutException">Timeout specified in <see cref="WikiClientBase.Timeout"/> has been reached.</exception>
        /// <returns>An <see cref="UploadResult"/>.</returns>
        /// <remarks>
        /// Upload from external source may take a while, so be sure to set a long <see cref="WikiClientBase.Timeout"/>
        /// in case the response from the server is delayed.
        /// </remarks>
        [Obsolete("Please use FilePage.UploadAsync instance methods instead.")]
        public static Task<UploadResult> UploadAsync(WikiSite site, string url, string title, string comment,
            bool ignoreWarnings, CancellationToken cancellationToken)
        {
            return new FilePage(site, title).UploadFromAsync(url, comment, ignoreWarnings,
                AutoWatchBehavior.Default, CancellationToken.None);
        }
        
        /// <summary>
        /// Asynchronously uploads a file, again.
        /// </summary>
        /// <param name="site">MedaiWiki site.</param>
        /// <param name="previousResult">An <see cref="UploadResult"/> returned from previous failed (or warned) upload.</param>
        /// <param name="title">Title of the file, with or without File: prefix.</param>
        /// <param name="comment">Comment of the upload, as well as the page content if it doesn't exist.</param>
        /// <param name="ignoreWarnings">Ignore any warnings. This must be set to upload a new version of an existing image.</param>
        /// <exception cref="UploadException">
        /// There's warning from server, and <paramref name="ignoreWarnings"/> is <c>false</c>.
        /// Check <see cref="UploadException.UploadResult"/> for the warning message or continuing the upload.
        /// </exception>
        /// <exception cref="UnauthorizedAccessException">You do not have the permission to upload the file.</exception>
        /// <exception cref="OperationFailedException"> There's an error while uploading the file. </exception>
        /// <exception cref="TimeoutException">Timeout specified in <see cref="WikiClientBase.Timeout"/> has been reached.</exception>
        /// <returns>An <see cref="UploadResult"/>.</returns>
        /// <remarks>
        /// You should have obtained the previous upload result via <see cref="UploadException.UploadResult"/>.
        /// </remarks>
        [Obsolete("Please use FilePage.UploadAsync instance methods instead.")]
        public static Task<UploadResult> UploadAsync(WikiSite site, UploadResult previousResult, string title,
            string comment, bool ignoreWarnings)
        {
            return new FilePage(site, title).UploadAsync(previousResult.FileKey, comment, ignoreWarnings,
                AutoWatchBehavior.Default, CancellationToken.None);
        }

        /// <summary>
        /// Asynchronously uploads a file.
        /// </summary>
        /// <param name="site">MedaiWiki site.</param>
        /// <param name="content">Content of the file.</param>
        /// <param name="title">Title of the file, with or without File: prefix.</param>
        /// <param name="comment">Comment of the upload, as well as the page content if it doesn't exist.</param>
        /// <param name="ignoreWarnings">Ignore any warnings. This must be set to upload a new version of an existing image.</param>
        /// <exception cref="UploadException">
        /// There's warning from server, and <paramref name="ignoreWarnings"/> is <c>false</c>.
        /// Check <see cref="UploadException.UploadResult"/> for the warning message or continuing the upload.
        /// </exception>
        /// <exception cref="UnauthorizedAccessException">You do not have the permission to upload the file.</exception>
        /// <exception cref="OperationFailedException"> There's an error while uploading the file. </exception>
        /// <exception cref="TimeoutException">Timeout specified in <see cref="WikiClientBase.Timeout"/> has been reached.</exception>
        /// <returns>An <see cref="UploadResult"/>.</returns>
        [Obsolete("Please use FilePage.UploadAsync instance methods instead.")]
        public static Task<UploadResult> UploadAsync(WikiSite site, Stream content, string title,
            string comment, bool ignoreWarnings)
        {
            return new FilePage(site, title).UploadAsync(content, comment, ignoreWarnings, AutoWatchBehavior.Default,
                CancellationToken.None);
        }

        /// <summary>
        /// Asynchronously uploads a file.
        /// </summary>
        /// <param name="site">MedaiWiki site.</param>
        /// <param name="content">Content of the file.</param>
        /// <param name="title">Title of the file, with or without File: prefix.</param>
        /// <param name="comment">Comment of the upload, as well as the page content if it doesn't exist.</param>
        /// <param name="ignoreWarnings">Ignore any warnings. This must be set to upload a new version of an existing image.</param>
        /// <param name="watch">Whether to add the file into your watchlist.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <exception cref="UploadException">
        /// There's warning from server, and <paramref name="ignoreWarnings"/> is <c>false</c>.
        /// Check <see cref="UploadException.UploadResult"/> for the warning message or continuing the upload.
        /// </exception>
        /// <exception cref="UnauthorizedAccessException">You do not have the permission to upload the file.</exception>
        /// <exception cref="OperationFailedException"> There's an error while uploading the file. </exception>
        /// <exception cref="TimeoutException">Timeout specified in <see cref="WikiClientBase.Timeout"/> has been reached.</exception>
        /// <returns>An <see cref="UploadResult"/>.</returns>
        [Obsolete("Please use FilePage.UploadAsync instance methods instead.")]
        public static Task<UploadResult> UploadAsync(WikiSite site, Stream content, string title,
            string comment, bool ignoreWarnings, AutoWatchBehavior watch, CancellationToken cancellationToken)
        {
            return new FilePage(site, title).UploadAsync(content, comment, ignoreWarnings, watch, cancellationToken);
        }

        /// <summary>
        /// Asynchronously uploads a file in this title.
        /// </summary>
        /// <param name="content">Content of the file.</param>
        /// <param name="comment">Comment of the upload, as well as the page content if it doesn't exist.</param>
        /// <param name="ignoreWarnings">Ignore any warnings. This must be set to upload a new version of an existing image.</param>
        /// <param name="watch">Whether to add the file into your watchlist.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <exception cref="UploadException">
        /// There's warning from server, and <paramref name="ignoreWarnings"/> is <c>false</c>.
        /// Check <see cref="UploadException.UploadResult"/> for the warning message or continuing the upload.
        /// </exception>
        /// <exception cref="UnauthorizedAccessException">You do not have the permission to upload the file.</exception>
        /// <exception cref="OperationFailedException"> There's an error while uploading the file. </exception>
        /// <exception cref="TimeoutException">Timeout specified in <see cref="WikiClientBase.Timeout"/> has been reached.</exception>
        /// <returns>An <see cref="UploadResult"/>.</returns>
        public Task<UploadResult> UploadAsync(Stream content, string comment, bool ignoreWarnings,
            AutoWatchBehavior watch, CancellationToken cancellationToken)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            return RequestHelper.UploadAsync(Site, "file", content, Title, comment, ignoreWarnings, watch,
                cancellationToken);
        }

        /// <inheritdoc cref="UploadAsync(Stream,string,bool,AutoWatchBehavior,CancellationToken)"/>
        public Task<UploadResult> UploadAsync(byte[] content, string comment, bool ignoreWarnings,
            AutoWatchBehavior watch, CancellationToken cancellationToken)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            using (var ms = new MemoryStream(content, false))
                return RequestHelper.UploadAsync(Site, "file", ms, Title, comment, ignoreWarnings, watch,
                    cancellationToken);
        }

        /// <inheritdoc cref="UploadAsync(Stream,string,bool,AutoWatchBehavior,CancellationToken)"/>
        /// <param name="fileKey">File key (or session key before MW1.17) of the previously stashed result.</param>
        public Task<UploadResult> UploadAsync(string fileKey, string comment, bool ignoreWarnings,
            AutoWatchBehavior watch, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(fileKey))
                throw new ArgumentException("Value cannot be null or empty.", nameof(fileKey));
            return RequestHelper.UploadAsync(Site,
                Site.SiteInfo.Version >= new Version(1, 18) ? "filekey" : "sessionkey",
                fileKey, Title, comment, ignoreWarnings, watch, cancellationToken);
        }

        /// <inheritdoc cref="UploadAsync(Stream,string,bool,AutoWatchBehavior,CancellationToken)"/>
        /// <param name="sourceUrl">The URL of the file to be uploaded.</param>
        public Task<UploadResult> UploadFromAsync(string sourceUrl, string comment, bool ignoreWarnings,
            AutoWatchBehavior watch, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(sourceUrl))
                throw new ArgumentException("Value cannot be null or empty.", nameof(sourceUrl));
            return RequestHelper.UploadAsync(Site, "url", sourceUrl, Title, comment, ignoreWarnings, watch,
                cancellationToken);
        }

        protected override void OnLoadPageInfo(JObject jpage)
        {
            base.OnLoadPageInfo(jpage);
            var jfile = jpage["imageinfo"];
            var lastRev = jfile.LastOrDefault();
            if (lastRev != null)
            {
                LastFileRevision = lastRev.ToObject<FileRevision>(Utility.WikiJsonSerializer);
            }
            else
            {
                // Possibly not a valid file.
                LastFileRevision = null;
            }
        }

        public FileRevision LastFileRevision { get; private set; }
    }

    /// <summary>
    /// Represents a revision of a file or image.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class FileRevision
    {
        /// <summary>
        /// The time and date of the revision.
        /// </summary>
        [JsonProperty]
        public DateTime TimeStamp { get; private set; }

        [JsonProperty("user")]
        public string UserName { get; private set; }

        [JsonProperty]
        public string Comment { get; private set; }

        /// <summary>
        /// Url of the file.
        /// </summary>
        [JsonProperty]
        public string Url { get; private set; }

        /// <summary>
        /// Url of the description page.
        /// </summary>
        [JsonProperty]
        public string DescriptionUrl { get; private set; }

        /// <summary>
        /// Size of the file. In bytes.
        /// </summary>
        [JsonProperty]
        public int Size { get; private set; }

        [JsonProperty]
        public int? Width { get; private set; }

        [JsonProperty]
        public int Height { get; private set; }

        /// <summary>
        /// The file's SHA-1 hash.
        /// </summary>
        [JsonProperty]
        public string Sha1 { get; private set; }
    }

    /// <summary>
    /// Contains the result from server after an upload operation.
    /// </summary>
    /// <remarks>See https://www.mediawiki.org/wiki/API:Upload .</remarks>
    [JsonObject(MemberSerialization.OptIn)]
    public class UploadResult
    {
        private static readonly Dictionary<string, string> UploadWarnings = new Dictionary<string, string>
        {
            // Referenced from pywikibot, site.py
            // map API warning codes to user error messages
            // {0} will be replaced by message string from API responsse
            {"duplicate-archive", "The file is a duplicate of a deleted file {0}."},
            {"was-deleted", "The file {0} was previously deleted."},
            {"emptyfile", "File {0} is empty."},
            {"exists", "File {0} already exists."},
            {"duplicate", "Uploaded file is a duplicate of {0}."},
            {"badfilename", "Target filename {0} is invalid."},
            {"filetype-unwanted-type", "File {0} type is unwanted type."},
            {"exists-normalized", "File exists with different extension as \"{0}\"."},
        };

        private static readonly KeyValuePair<string, string>[] EmptyWarnings = { };

        private static readonly DateTime[] EmptyDateTime = { };

        /// <summary>
        /// Try to convert the specified warning code and context into a user-fridendly
        /// warning message.
        /// </summary>
        /// <param name="warningCode">Case-sensitive warning code.</param>
        /// <param name="context">The extra content of the warning.</param>
        /// <returns>
        /// It tries to match the warningCode with well-known ones, and returns a
        /// user-fridendly warning message. If there's no match, a string containing
        /// warningCode and context will be returned.
        /// </returns>
        public static string FormatWarning(string warningCode, string context)
        {
            string msg;
            if (UploadWarnings.TryGetValue(warningCode, out msg))
                return string.Format(msg, context);
            return $"{warningCode}: {context}";
        }

        /// <summary>
        /// A brief word describing the result of the operation.
        /// </summary>
        public UploadResultCode ResultCode { get; private set; }

        [JsonProperty("result")]
        private string Result
        {
            set
            {
                switch (value)
                {
                    case "Success":
                        ResultCode = UploadResultCode.Success;
                        break;
                    case "Warning":
                        ResultCode = UploadResultCode.Warning;
                        break;
                    case "Continue":
                        ResultCode = UploadResultCode.Continue;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown result: " + value);
                }
            }
        }

        /// <summary>
        /// For <see cref="UploadResultCode.Warning"/> and <see cref="UploadResultCode.Continue"/>,
        /// the file key to be passed into the next upload attempt. 
        /// </summary>
        [JsonProperty]
        public string FileKey { get; private set; }

        // Same as filekey, maintained for backward compatibility (deprecated in 1.18)
        [JsonProperty]
        private string SessionKey
        {
            set
            {
                if (FileKey == null) FileKey = value;
                else Debug.Assert(FileKey == value);
            }
        }

        /// <summary>
        /// Gets a list of key-value pairs, indicating the warning code and its context.
        /// </summary>
        /// <value>
        /// A readonly list of warning code - context pairs.
        /// The list is guaranteed not to be <c>null</c>,
        /// but it can be empty.
        /// </value>
        /// <remarks>
        /// <para>You can use <see cref="FormatWarning"/> to get user-friendly warning messages.</para>
        /// <para>If you have supressed warnings, the warnings will still be here, but <see cref="ResultCode"/> will be <see cref="UploadResultCode.Success"/>.</para>
        /// </remarks>
        public IList<KeyValuePair<string, string>> Warnings { get; private set; } = EmptyWarnings;

        /// <summary>
        /// Gets a list of timestamps indicating the duplicate file versions, if any.
        /// </summary>
        public IList<DateTime> DuplicateVersions { get; private set; } = EmptyDateTime;

        [JsonProperty("warnings")]
        private JObject RawWarnings
        {
            set
            {
                var l = value.Properties()
                    .Where(p => p.Value.Type == JTokenType.String)
                    .Select(p => new KeyValuePair<string, string>(p.Name, (string) p.Value)).ToList();
                Warnings = new ReadOnlyCollection<KeyValuePair<string, string>>(l);
                var dv = value["duplicateversions"];
                if (dv != null)
                {
                    var m = dv.Children<JProperty>().Where(p => p.Name == "timestamp")
                        .Select(p => (DateTime) p.Value).ToList();
                    DuplicateVersions = new ReadOnlyCollection<DateTime>(m);
                }
            }
        }

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        /// <returns>
        /// 表示当前对象的字符串。
        /// </returns>
        public override string ToString()
        {
            return $"{ResultCode}; {string.Join(",", Warnings.Select(p => p.Key + "=" + p.Value))}";
        }
    }

    /// <summary>
    /// General results of an upload operation.
    /// </summary>
    public enum UploadResultCode
    {
        Success = 0,
        Warning,
        Continue
    }
}
