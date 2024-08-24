using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WikiClientLibrary.Client;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Infrastructures.Logging;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Files;

public static class WikiSiteExtensions
{

    /// <inheritdoc cref="UploadAsync(WikiSite,string,WikiUploadSource,string,bool,AutoWatchBehavior,CancellationToken)"/>
    public static Task<UploadResult> UploadAsync(this WikiSite site, string title, WikiUploadSource source, string? comment,
        bool ignoreWarnings)
    {
        return UploadAsync(site, title, source, comment, ignoreWarnings, AutoWatchBehavior.Default, CancellationToken.None);
    }

    /// <inheritdoc cref="UploadAsync(WikiSite,string,WikiUploadSource,string,bool,AutoWatchBehavior,CancellationToken)"/>
    public static Task<UploadResult> UploadAsync(this WikiSite site, string title, WikiUploadSource source, string? comment,
        bool ignoreWarnings,
        AutoWatchBehavior watch)
    {
        return UploadAsync(site, title, source, comment, ignoreWarnings, watch, CancellationToken.None);
    }

    /// <summary>
    /// Asynchronously uploads a file in this title.
    /// </summary>
    /// <param name="site"></param>
    /// <param name="title"></param>
    /// <param name="source">Source of the file.</param>
    /// <param name="comment">Comment of the upload, as well as the page content if it doesn't exist.</param>
    /// <param name="ignoreWarnings">Ignore any warnings. This must be set to upload a new version of an existing image.</param>
    /// <param name="watch">Whether to add the file into your watchlist.</param>
    /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
    /// <exception cref="UnauthorizedAccessException">You do not have the permission to upload the file.</exception>
    /// <exception cref="OperationFailedException">
    /// There's an general failure while uploading the file.
    /// - or -
    /// Since MW 1.31, if you are uploading the exactly same content to the same title
    /// with <paramref name="ignoreWarnings"/> set to <c>true</c>,
    /// you will receive this exception with <see cref="OperationFailedException.ErrorCode"/>
    /// set to <c>fileexists-no-change</c>. See <a href="https://gerrit.wikimedia.org/r/378702">gerrit:378702</a>.
    /// </exception>
    /// <exception cref="TimeoutException">Timeout specified in <see cref="WikiClient.Timeout"/> has been reached.</exception>
    /// <returns>An <see cref="UploadResult"/>. You need to check <see cref="UploadResult.ResultCode"/> for further action.</returns>
    public static async Task<UploadResult> UploadAsync(this WikiSite site, string title, WikiUploadSource source, string? comment,
        bool ignoreWarnings,
        AutoWatchBehavior watch, CancellationToken cancellationToken)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        Debug.Assert(source != null);
        var link = WikiLink.Parse(site, title, BuiltInNamespaces.File);
        using (site.BeginActionScope(null, title, source))
        {
            var requestFields = new OrderedKeyValuePairs<string, object?>
            {
                { "action", "upload" },
                { "watchlist", watch },
                { "token", WikiSiteToken.Edit },
                { "filename", link.Title },
                { "comment", comment },
                { "ignorewarnings", ignoreWarnings },
            };
            foreach (var p in source.GetUploadParameters(site.SiteInfo))
                requestFields[p.Key] = p.Value;
            var request = new MediaWikiFormRequestMessage(requestFields, true);
            site.Logger.LogDebug("Start uploading.");
            var jresult = await site.InvokeMediaWikiApiAsync2(request, cancellationToken);
            var result = jresult["upload"].Deserialize<UploadResult>(MediaWikiHelper.WikiJsonSerializerOptions);
            site.Logger.LogInformation("Uploaded. Result={Result}.", result.ResultCode);
            return result;
        }
    }

}
