using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Infrastructures.Logging;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Wikia.Sites;

/// <summary>
/// Represents a Wikia site endpoint.
/// </summary>
/// <remarks>
/// <para>
/// For FANDOM/Wikia specific site access, use this instead of <see cref="WikiSite"/> base class,
/// because this class contains more Wikia-specific APIs, and properly handles Wikia-specific API peculiarities
/// (such as user logout).
/// </para>
/// <para>See <see cref="WikiClientLibrary.Wikia"/> for a summary on FANDOM-and-Wikia.org-specific APIs.</para>
/// </remarks>
public class WikiaSite : WikiSite
{

    internal static readonly MediaWikiVersion mw119Version = new MediaWikiVersion(1, 19);

    protected new WikiaSiteOptions Options => (WikiaSiteOptions)base.Options;

    /// <inheritdoc />
    /// <summary>
    /// Initializes a new <see cref="WikiaSite"/> instance from the Wikia site root URL.
    /// </summary>
    /// <param name="siteRootUrl">Wikia site root URL, with the ending slash. e.g. <c>http://community.wikia.com/</c>.</param>
    public WikiaSite(IWikiClient wikiClient, string siteRootUrl) : this(wikiClient, new WikiaSiteOptions(siteRootUrl), null, null)
    {
    }

    /// <inheritdoc />
    public WikiaSite(IWikiClient wikiClient, WikiaSiteOptions options) : this(wikiClient, options, null, null)
    {
    }

    /// <inheritdoc />
    public WikiaSite(IWikiClient wikiClient, WikiaSiteOptions options, string userName, string password)
        : base(wikiClient, options, userName, password)
    {
    }

    public SiteVariableData WikiVariables { get; private set; }

    /// <inheritdoc cref="InvokeWikiaAjaxAsync{T}(WikiRequestMessage,IWikiResponseMessageParser{T},CancellationToken)"/>
    /// <remarks>
    /// <para>This overload uses <see cref="WikiaJsonResponseParser"/> to parse the response.</para>
    /// <para>This method will automatically add <c>action=ajax</c> field in the request.</para>
    /// </remarks>
    public async Task<JsonNode> InvokeWikiaAjaxAsync(WikiRequestMessage request, CancellationToken cancellationToken)
    {
        return await InvokeWikiaAjaxAsync(request, WikiaJsonResponseParser.Default, cancellationToken);
    }

    /// <summary>
    /// Invokes <c>index.php</c> call with <c>action=ajax</c> query.
    /// </summary>
    /// <param name="request">The request message.</param>
    /// <param name="responseParser">The parser used to parse the response.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The parsed JSON root of response.</returns>
    /// <exception cref="ArgumentNullException">Either <paramref name="request"/> or <paramref name="responseParser"/> is <c>null</c>.</exception>
    /// <remarks>This method will automatically add <c>action=ajax</c> field in the request.</remarks>
    public async Task<T> InvokeWikiaAjaxAsync<T>(WikiRequestMessage request,
        IWikiResponseMessageParser<T> responseParser, CancellationToken cancellationToken)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (responseParser == null) throw new ArgumentNullException(nameof(responseParser));
        var localRequest = request;
        if (request is WikiaQueryRequestMessage queryRequest)
        {
            var fields = new List<KeyValuePair<string, object>>(queryRequest.Fields.Count + 1)
            {
                new KeyValuePair<string, object>("action", "ajax")
            };
            fields.AddRange(queryRequest.Fields);
            localRequest = new WikiaQueryRequestMessage(request.Id, fields, queryRequest.UseHttpPost);
        }
        Logger.LogDebug("Invoking Wikia Ajax: {Request}", localRequest);
        var result = await WikiClient.InvokeAsync(Options.ScriptUrl, localRequest,
            responseParser, cancellationToken);
        return result;
    }

    /// <inheritdoc cref="InvokeNirvanaAsync{T}(WikiRequestMessage,IWikiResponseMessageParser{T},CancellationToken)"/>
    /// <remarks>This overload uses <see cref="WikiaJsonResponseParser"/> to parse the response.</remarks>
    public Task<JsonNode> InvokeNirvanaAsync(WikiRequestMessage request, CancellationToken cancellationToken)
    {
        return InvokeNirvanaAsync(request, WikiaJsonResponseParser.Default, cancellationToken);
    }

    /// <summary>
    /// Invokes nirvana API call.
    /// </summary>
    /// <param name="request">The request message.</param>
    /// <param name="responseParser">The parser used to parse the response.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <exception cref="ArgumentNullException">Either <paramref name="request"/> or <paramref name="responseParser"/> is <c>null</c>.</exception>
    /// <returns>The parsed JSON root of response.</returns>
    /// <seealso cref="WikiaSiteOptions.NirvanaEndPointUrl"/>
    public async Task<T> InvokeNirvanaAsync<T>(WikiRequestMessage request,
        IWikiResponseMessageParser<T> responseParser, CancellationToken cancellationToken)
    {
        Logger.LogDebug("Invoking Nirvana API: {Request}", request);
        var result = await WikiClient.InvokeAsync(Options.NirvanaEndPointUrl, request, responseParser, cancellationToken);
        return result;
    }

    /// <inheritdoc cref="InvokeWikiaApiAsync{T}(string,WikiRequestMessage,IWikiResponseMessageParser{T},CancellationToken)"/>
    /// <remarks>This overload uses <see cref="WikiaJsonResponseParser"/> to parse the response.</remarks>
    public Task<JsonNode> InvokeWikiaApiAsync(string relativeUri, WikiRequestMessage request, CancellationToken cancellationToken)
    {
        return InvokeWikiaApiAsync(relativeUri, request, WikiaJsonResponseParser.Default, cancellationToken);
    }

    /// <summary>
    /// Invokes Wikia API v1 call.
    /// </summary>
    /// <param name="relativeUri">The URI relative to <see cref="WikiaSiteOptions.WikiaApiRootUrl"/>. It often starts with a slash.</param>
    /// <param name="request">The request message.</param>
    /// <param name="responseParser">The parser used to parse the response.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <exception cref="ArgumentNullException">Either <paramref name="request"/> or <paramref name="responseParser"/> is <c>null</c>.</exception>
    /// <returns>The parsed JSON root of response.</returns>
    public async Task<T> InvokeWikiaApiAsync<T>(string relativeUri, WikiRequestMessage request,
        IWikiResponseMessageParser<T> responseParser, CancellationToken cancellationToken)
    {
        Logger.LogDebug("Invoking Wikia API v1: {Request}", request);
        var result = await WikiClient.InvokeAsync(Options.WikiaApiRootUrl + relativeUri, request, responseParser, cancellationToken);
        return result;
    }

    /// <inheritdoc />
    public override async Task RefreshSiteInfoAsync()
    {
        await base.RefreshSiteInfoAsync();
        using (this.BeginActionScope(null))
        {
            var jresult = await InvokeWikiaApiAsync("/Mercury/WikiVariables", new WikiaQueryRequestMessage(), CancellationToken.None);
            var jdata = jresult["data"];
            if (jdata == null) throw new UnexpectedDataException("Missing data node in the JSON response.");
            WikiVariables = jdata.Deserialize<SiteVariableData>(MediaWikiHelper.WikiJsonSerializerOptions);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// To logout the user, this override sends a POST request to <c>https://www.wikia.com/logout</c>.
    /// </remarks>
    protected override Task SendLogoutRequestAsync()
    {
        if (SiteInfo.Version > mw119Version)
        {
            // After MW version upgrade, Wikia sites should accept
            return base.SendLogoutRequestAsync();
        }

        this.Logger.LogInformation("Using legacy (Wikia MW 1.19 fork) logout approach.");
        return MW119LogoutAsync();

        async Task MW119LogoutAsync()
        {
            string logoutUrl;
            if (SiteInfo.ServerUrl.EndsWith(".wikia.com", StringComparison.OrdinalIgnoreCase))
            {
                logoutUrl = "https://www.wikia.com/logout";
            }
            else if (SiteInfo.ServerUrl.EndsWith(".wikia.org", StringComparison.OrdinalIgnoreCase))
            {
                logoutUrl = "https://www.wikia.org/logout";
            }
            else if (SiteInfo.ServerUrl.EndsWith(".fandom.com", StringComparison.OrdinalIgnoreCase))
            {
                logoutUrl = "https://www.fandom.com/logout";
            }
            else
            {
                logoutUrl = MediaWikiHelper.MakeAbsoluteUrl(SiteInfo.ServerUrl, "logout");
                // User is using WikiaSite instance outside Wikia… I wish you good luck.
                this.Logger.LogWarning("WikiaSite is instantiated with a non-FANDOM site URL: {Url}. Assuming logout URL as {LogoutUrl}.",
                    SiteInfo.ServerUrl, logoutUrl);
            }
            await WikiClient.InvokeAsync(logoutUrl,
                new MediaWikiFormRequestMessage(new { redirect = "" }),
                DiscardingResponseMessageParser.Instance,
                CancellationToken.None);
        }
    }

    /// <summary>
    /// This WikiResponseMessageParser implementation validates the HTTP status code, and discards the response content.
    /// </summary>
    private class DiscardingResponseMessageParser : WikiResponseMessageParser<object>
    {

        private static readonly Task<object> dummyResult = Task.FromResult(new object());

        public static readonly DiscardingResponseMessageParser Instance = new DiscardingResponseMessageParser();

        /// <inheritdoc />
        public override Task<object> ParseResponseAsync(HttpResponseMessage response, WikiResponseParsingContext context)
        {
            var responseCode = (int)response.StatusCode;
            if (responseCode >= 300 && responseCode <= 399 || response.IsSuccessStatusCode) return dummyResult;

            context.NeedRetry = true;
            response.EnsureSuccessStatusCode(); // An exception will be thrown here.
            return dummyResult;
        }

    }

}
