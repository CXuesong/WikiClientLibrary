using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Infrastructures.Logging;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Pages
{
    /// <summary>
    /// A <see cref="WikiUploadSource"/> that enables chunked stashing and performs final upload by <c>filekey</c>. (MW 1.19+)
    /// </summary>
    /// <remarks>
    /// <para>
    /// Since uploading a huge file in a single HTTP POST can be unreliable,
    /// MW upload API also supports a chunked upload mode,
    /// where you make multiple requests with portions of the file.
    /// This is available in MediaWiki 1.20 and above, although prior to version 1.25,
    /// SVGs could not be uploaded via chunked uploading.
    /// </para>
    /// <para>See https://www.mediawiki.org/wiki/API:Upload#Chunked_uploading .</para>
    /// <para>
    /// Before you can use this class with
    /// <see cref="FilePage.UploadAsync(WikiUploadSource,string,bool,AutoWatchBehavior,CancellationToken)"/>,
    /// you need to stash the whole stream in chunks to the server with <see cref="StashNextChunkAsync()"/>
    /// or its overloads.
    /// </para>
    /// </remarks>
    public class ChunkedUploadSource : WikiUploadSource
    {

        private static readonly Version v118 = new Version(1, 18);

        private readonly long originalSourceStreamPosition;
        private int state;
        private string lastStashingFileKey;

        private const int STATE_CHUNK_IMPENDING = 0;
        private const int STATE_CHUNK_STASHING = 1;
        private const int STATE_ALL_STASHED = 2;

        /// <inheritdoc cref="ChunkedUploadSource(WikiSite,Stream,string)"/>
        public ChunkedUploadSource(WikiSite site, Stream sourceStream) : this(site, sourceStream, null)
        {
        }

        /// <summary>
        /// Initialize an instance from a <see cref="Stream"/>.
        /// </summary>
        /// <param name="site">The destination site to upload the file.</param>
        /// <param name="sourceStream">
        /// The seekable stream containing the file to be uploaded.
        /// The upload will be performed starting from the current position.
        /// </param>
        /// <param name="fileName">
        /// Optional file name. This parameter can be <c>null</c>, where a dummy file name will be used;
        /// otherwise, it should be a valid file name.
        /// If the name has file extension, the content will be validated by server.
        /// However, the name can be different from the actual uploaded file.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="site"/> or <paramref name="sourceStream"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="sourceStream"/> does not support seeking
        /// - or -
        /// <paramref name="sourceStream"/> has met EOF.
        /// </exception>
        public ChunkedUploadSource(WikiSite site, Stream sourceStream, string fileName)
        {
            Site = site ?? throw new ArgumentNullException(nameof(site));
            SourceStream = sourceStream ?? throw new ArgumentNullException(nameof(sourceStream));
            if (!sourceStream.CanSeek)
                throw new ArgumentException("The stream does not support seeking.", nameof(sourceStream));
            originalSourceStreamPosition = SourceStream.Position;
            TotalSize = (int) (SourceStream.Length - originalSourceStreamPosition);
            if (TotalSize == 0) throw new ArgumentException("Cannot upload empty stream.");
            // Upload 1MB chunks by default.
            DefaultChunkSize = 1024 * 1024;
            if (site.SiteInfo.MinUploadChunkSize > 0 && site.SiteInfo.MinUploadChunkSize > DefaultChunkSize)
                DefaultChunkSize = site.SiteInfo.MinUploadChunkSize;
            else if (site.SiteInfo.MaxUploadSize > 0 && site.SiteInfo.MaxUploadSize < DefaultChunkSize)
                DefaultChunkSize = (int) site.SiteInfo.MaxUploadSize;
            FileName = fileName ?? "Dummy";
        }

        public WikiSite Site { get; }

        /// <summary>
        /// The source stream containing the content to be uploaded.
        /// </summary>
        public Stream SourceStream { get; }

        /// <summary>
        /// The file name used in the stashing requests.
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// The chunk size to be used when calling the <see cref="StashNextChunkAsync()"/> overloads
        /// without explicitly specifying chunk size.
        /// </summary>
        /// <remarks>
        /// By default this is 1MB, and the default value is snapped
        /// between the limits specified in <see cref="SiteInfo"/>.
        /// </remarks>
        public int DefaultChunkSize { get; set; }

        /// <summary>
        /// Gets the size of the uploaded part of file.
        /// </summary>
        public int UploadedSize { get; private set; }

        /// <summary>
        /// Gets the total size of the file to be uploaded.
        /// </summary>
        /// <remarks>
        /// The total size is calculated upon the uploading of the first chunk as
        /// <c>SourceStream.Length - SourceStream.Position</c>.
        /// </remarks>
        public int TotalSize { get; }

        /// <summary>
        /// Determines whether a chunk is currently uploading for stashing.
        /// </summary>
        public bool IsStashing => state == STATE_CHUNK_STASHING;

        /// <summary>
        /// Determins whether the file has been stashed completely.
        /// </summary>
        public bool IsStashed => state == STATE_ALL_STASHED;

        /// <summary>
        /// When <see cref="IsStashed"/> is <c>true</c>, gets the filekey used for file upload.
        /// </summary>
        public string FileKey { get; private set; }

        /// <inheritdoc />
        public override IEnumerable<KeyValuePair<string, object>> GetUploadParameters(SiteInfo siteInfo)
        {
            if (siteInfo == null) throw new ArgumentNullException(nameof(siteInfo));
            if (state != STATE_ALL_STASHED)
                throw new InvalidOperationException("Cannot upload the file before all the chunks has been stashed.");
            Debug.Assert(FileKey != null);
            return new[]
            {
                new KeyValuePair<string, object>(
                    siteInfo.Version >= v118 ? "filekey" : "sessionkey", FileKey)
            };
        }

        public Task<UploadResult> StashNextChunkAsync()
        {
            return StashNextChunkAsync(DefaultChunkSize, new CancellationToken());
        }

        public Task<UploadResult> StashNextChunkAsync(int chunkSize)
        {
            return StashNextChunkAsync(chunkSize, new CancellationToken());
        }

        public Task<UploadResult> StashNextChunkAsync(CancellationToken cancellationToken)
        {
            return StashNextChunkAsync(DefaultChunkSize, cancellationToken);
        }

        /// <summary>
        /// Stash the next chunk in the stream.
        /// </summary>
        /// <param name="chunkSize">The maximum size of the next chunk.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="chunkSize"/> is not between <see cref="SiteInfo.MinUploadChunkSize"/> and <see cref="SiteInfo.MaxUploadSize"/>.</exception>
        /// <exception cref="InvalidOperationException">A chunk is currently uploading - or - <see cref="TotalSize"/> is zero.</exception>
        /// <exception cref="OperationFailedException">
        /// General operation failure - or - specified as follows
        /// <list type="table">
        /// <listheader>
        /// <term><see cref="OperationFailedException.ErrorCode"/></term>
        /// <description>Description</description>
        /// </listheader>
        /// <item>
        /// <term>illegal-filename</term>
        /// <description>The filename is not allowed.</description>
        /// </item>
        /// <item>
        /// <term>stashfailed</term>
        /// <description>Stash failure. Can be caused by file verification failure. (e.g. Extension of the file name does not match the file content.)</description>
        /// </item>
        /// </list>
        /// </exception>
        /// <returns>
        /// <c>true</c> if a chunk has been uploaded;
        /// <c>false</c> if all the chunks has already been uploaded.
        /// </returns>
        /// <remarks>
        /// </remarks>
        public async Task<UploadResult> StashNextChunkAsync(int chunkSize, CancellationToken cancellationToken)
        {
            var minChunkSize = Site.SiteInfo.MinUploadChunkSize;
            var maxChunkSize = Site.SiteInfo.MaxUploadSize;
            if (chunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize));
            // For Wikia (MW 1.19), it supports chunked uploading,
            // while SiteInfo.MinUploadChunkSize and SiteInfo.MaxUploadSize are missing.
            if (minChunkSize > 0 && chunkSize < minChunkSize || maxChunkSize > 0 && chunkSize > maxChunkSize)
                throw new ArgumentOutOfRangeException(nameof(chunkSize),
                    $"Chunk size should be between {minChunkSize} and {maxChunkSize} on this wiki site.");
            var lastState = Interlocked.CompareExchange(ref state, STATE_CHUNK_STASHING, STATE_CHUNK_IMPENDING);
            switch (lastState)
            {
                case STATE_CHUNK_STASHING:
                    throw new InvalidOperationException("Cannot concurrently upload two chunks.");
                case STATE_ALL_STASHED:
                    throw new InvalidOperationException("The content has been uploaded.");
            }
            var startingPos = SourceStream.Position;
            using (Site.BeginActionScope(this, chunkSize))
            {
                RETRY:
                try
                {
                    UploadResult result;
                    Site.Logger.LogDebug("Start uploading chunk of {Stream} from offset {Offset}/{TotalSize}.",
                        SourceStream, UploadedSize, TotalSize);
                    using (var chunkStream = new MemoryStream((int)Math.Min(chunkSize, SourceStream.Length - startingPos)))
                    {
                        var copiedSize = await SourceStream.CopyRangeToAsync(chunkStream, chunkSize, cancellationToken);
                        // If someone has messed with the SourceStream, this can happen.
                        if (copiedSize == 0)
                            throw new InvalidOperationException("Unexpected stream EOF met.");
                        chunkStream.Position = 0;
                        var jparams = new Dictionary<string, object>
                        {
                            {"action", "upload"},
                            {"token", WikiSiteToken.Edit},
                            {"filename", FileName},
                            {"filekey", lastStashingFileKey},
                            {"offset", UploadedSize},
                            {"filesize", TotalSize},
                            {"comment", "Chunked"},
                            {"stash", true},
                            {"ignorewarnings", true},
                            {"chunk", chunkStream},
                        };
                        var message = new WikiFormRequestMessage(jparams, true);
                        message.ApiErrorRaised += (_, e) =>
                        {
                            // Possible error: code=stashfailed, info=Invalid chunk offset, offset=xxxx
                            // We will try to recover.
                            if (e.ErrorCode == "stashfailed" && e.ErrorNode["offset"] != null)
                                e.Handled = true;
                        };
                        var jresult = await Site.GetJsonAsync(message, cancellationToken);
                        // Possible error: code=stashfailed, info=Invalid chunk offset
                        // We will retry from the server-expected offset.
                        var err = jresult["error"];
                        if (err != null && (string)err["code"] == "stashfailed" && err["offset"] != null)
                        {
                            Site.Logger.LogWarning("Server reported: {Message}. Will retry from offset {Offset}.",
                                (string)err["info"], (int)err["offset"]);
                            UploadedSize = (int)err["offset"];
                            goto RETRY;
                        }
                        result = jresult["upload"].ToObject<UploadResult>(Utility.WikiJsonSerializer);
                        // Ignore warnings, as long as we have filekey to continue the upload.
                        if (result.FileKey == null)
                        {
                            Debug.Assert(result.ResultCode != UploadResultCode.Warning);
                            throw new UnexpectedDataException("Expect [filekey] or [sessionkey] in upload result. Found none.");
                        }
                        // Note the fileKey changes after each upload.
                        lastStashingFileKey = result.FileKey;
                        UploadedSize += copiedSize;
                        if (result.Offset != null && result.Offset != UploadedSize)
                        {
                            Site.Logger.LogWarning(
                                "Unexpected next chunk offset reported from server: {ServerUploadedSize}. Expect: {UploadedSize}. Will use the server-reported offset.",
                                result.Offset, UploadedSize);
                            UploadedSize = (int)result.Offset.Value;
                            SourceStream.Position = originalSourceStreamPosition + UploadedSize;
                        }
                    }
                    Site.Logger.LogDebug("Uploaded chunk of {Stream}. Offset: {UploadedSize}/{TotalSize}, Result: {Result}.",
                        SourceStream, UploadedSize, TotalSize, result.ResultCode);
                    if (result.ResultCode == UploadResultCode.Success)
                    {
                        state = STATE_ALL_STASHED;
                        FileKey = result.FileKey;
                        lastStashingFileKey = null;
                    }
                    return result;
                }
                catch (Exception)
                {
                    // Restore stream position upon error.
                    SourceStream.Position = startingPos;
                    throw;
                }
                finally
                {
                    Interlocked.CompareExchange(ref state, STATE_CHUNK_IMPENDING, STATE_CHUNK_STASHING);
                }
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return "ChunkedUploadSource(" + SourceStream + ")";
        }
        
    }
}
