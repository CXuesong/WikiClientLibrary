using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages;

namespace WikiClientLibrary.Sites
{
    /// <summary>
    /// Represents a MediaWiki site.
    /// </summary>
    public partial class WikiSite : IWikiClientLoggable
    {

        private readonly SiteOptions options;

        #region Services

        /// <summary>
        /// Gets the <see cref="WikiClientBase" /> used to perform the requests.
        /// </summary>
        public WikiClientBase WikiClient { get; }

        /// <summary>
        /// A handler used to re-login when account assertion fails.
        /// </summary>
        public IAccountAssertionFailureHandler AccountAssertionFailureHandler { get; set; }

        private Throttler _ModificationThrottler;
        private readonly TokensManager tokensManager;

        /// <summary>
        /// A throttler used to enforce the speed limitation when performing edit/move/delete operations.
        /// </summary>
        public Throttler ModificationThrottler
        {
            get
            {
                return LazyInitializer.EnsureInitialized(ref _ModificationThrottler, () =>
                {
                    var t = new Throttler {LoggerFactory = LoggerFactory};
                    return t;
                });
            }
            set { _ModificationThrottler = value; }
        }

        #endregion

        /// <summary>
        /// Initialize a <see cref="WikiSite"/> instance with the given API Endpoint URL.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="wikiClient"/> or <paramref name="apiEndpoint"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="apiEndpoint"/> is invalid.</exception>
        /// <exception cref="UnauthorizedOperationException">Cannot access query API module due to target site permission settings. You may take a look at <see cref="SiteOptions.ExplicitInfoRefresh"/>.</exception>
        public static Task<WikiSite> CreateAsync(WikiClientBase wikiClient, string apiEndpoint)
        {
            if (wikiClient == null) throw new ArgumentNullException(nameof(wikiClient));
            if (apiEndpoint == null) throw new ArgumentNullException(nameof(apiEndpoint));
            return CreateAsync(wikiClient, new SiteOptions(apiEndpoint));
        }

        /// <summary>
        /// Initialize a <see cref="WikiSite"/> instance with the specified settings.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="wikiClient"/> or <paramref name="options"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">One or more settings in <paramref name="options"/> is invalid.</exception>
        /// <exception cref="UnauthorizedOperationException">Cannot access query API module due to target site permission settings. You may take a look at <see cref="SiteOptions.ExplicitInfoRefresh"/>.</exception>
        public static async Task<WikiSite> CreateAsync(WikiClientBase wikiClient, SiteOptions options)
        {
            if (wikiClient == null) throw new ArgumentNullException(nameof(wikiClient));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrEmpty(options.ApiEndpoint))
                throw new ArgumentException("Invalid API endpoint url.", nameof(options));
            var site = new WikiSite(wikiClient, options) {LoggerFactory = wikiClient.LoggerFactory};
            if (!options.ExplicitInfoRefresh)
                await Task.WhenAll(site.RefreshSiteInfoAsync(), site.RefreshAccountInfoAsync());
            return site;
        }

        /// <summary>
        /// Given a site or page URL of a MediaWiki site, try to look for the Api Endpoint URL of it.
        /// </summary>
        /// <param name="client">WikiClient instance.</param>
        /// <param name="urlExpression">The URL of MediaWiki site. It can be with or without protocol prefix.</param>
        /// <exception cref="ArgumentNullException"><paramref name="client"/> or <paramref name="urlExpression"/> is <c>null</c>.</exception>
        /// <exception cref="TimeoutException">A time-out has been reached during test requests.</exception>
        /// <returns>The URL of Api Endpoint. OR <c>null</c> if such search has failed.</returns>
        public static Task<string> SearchApiEndpointAsync(WikiClient client, string urlExpression)
        {
            var logger = client.LoggerFactory == null
                ? NullLogger.Instance
                : (ILogger) client.LoggerFactory.CreateLogger<WikiSite>();
            return MediaWikiUtility.SearchApiEndpointAsync(client, urlExpression, logger);
        }

        protected WikiSite(WikiClientBase wikiClient, SiteOptions options)
        {
            // Perform basic checks.
            Debug.Assert(wikiClient != null);
            Debug.Assert(options != null);
            WikiClient = wikiClient;
            this.options = options.Clone();
            tokensManager = new TokensManager(this);
            DisambiguationTemplatesAsync = new AsyncLazy<ICollection<string>>(async () =>
            {
                if (this.options.DisambiguationTemplates == null)
                {
                    var dabPages = await RequestHelper
                        .EnumLinksAsync(this, "MediaWiki:Disambiguationspage", new[] {BuiltInNamespaces.Template})
                        .ToList();
                    if (dabPages.Count == 0)
                    {
                        // Try to fetch from mw messages
                        var msg = await GetMessageAsync("disambiguationspage");
                        if (msg != null) dabPages.Add(msg);
                    }
                    dabPages.Add(SiteOptions.DefaultDisambiguationTemplate);
                    return dabPages;
                }
                return this.options.DisambiguationTemplates;
            });
        }

        /// <summary>
        /// Refreshes site information.
        /// </summary>
        /// <returns>
        /// This method affects <see cref="SiteInfo"/>, <see cref="Namespaces"/>,
        /// <see cref="InterwikiMap"/>, and <see cref="Extensions"/> properties.
        /// </returns>
        /// <exception cref="UnauthorizedOperationException">Cannot access query API module due to target site permission settings. You may need to login first.</exception>
        public async Task RefreshSiteInfoAsync()
        {
            var jobj = await GetJsonAsync(new WikiFormRequestMessage(new
            {
                action = "query",
                meta = "siteinfo",
                siprop = "general|namespaces|namespacealiases|interwikimap|extensions"
            }), true, CancellationToken.None);
            var qg = (JObject) jobj["query"]["general"];
            var ns = (JObject) jobj["query"]["namespaces"];
            var aliases = (JArray) jobj["query"]["namespacealiases"];
            var interwiki = (JArray) jobj["query"]["interwikimap"];
            var extensions = (JArray) jobj["query"]["extensions"];
            try
            {
                _SiteInfo = qg.ToObject<SiteInfo>(Utility.WikiJsonSerializer);
                _Namespaces = new NamespaceCollection(this, ns, aliases);
                _InterwikiMap = new InterwikiMap(this, interwiki);
                _Extensions = new ExtensionCollection(this, extensions);
            }
            catch (Exception)
            {
                // Reset the state so that AssertSiteInitialized will work properly.
                _SiteInfo = null;
                _Namespaces = null;
                _InterwikiMap = null;
                _Extensions = null;
                throw;
            }
        }

        /// <summary>
        /// Asserts that <see cref="SiteInfo"/> has been loaded.
        /// </summary>
        private void AssertSiteInfoInitialized()
        {
            if (_SiteInfo == null)
                throw new InvalidOperationException(
                    "Site.RefreshSiteInfoAsync should be successfully invoked before performing the operation.");
        }

        /// <summary>
        /// Refreshes user account information.
        /// </summary>
        /// <remarks>
        /// This method affects <see cref="AccountInfo"/> property.
        /// </remarks>
        /// <exception cref="UnauthorizedOperationException">Cannot access query API module due to target site permission settings. You may need to login first.</exception>
        public async Task RefreshAccountInfoAsync()
        {
            var jobj = await GetJsonAsync(new WikiFormRequestMessage(new
            {
                action = "query",
                meta = "userinfo",
                uiprop = "blockinfo|groups|hasmsg|rights"
            }), true, CancellationToken.None);
            _AccountInfo = ((JObject) jobj["query"]["userinfo"]).ToObject<AccountInfo>(Utility.WikiJsonSerializer);
            ListingPagingSize = _AccountInfo.HasRight(UserRights.ApiHighLimits) ? 5000 : 500;
        }

        private SiteInfo _SiteInfo;
        private AccountInfo _AccountInfo;
        private NamespaceCollection _Namespaces;
        private InterwikiMap _InterwikiMap;
        private ExtensionCollection _Extensions;

        /// <summary>
        /// Gets the basic site information.
        /// </summary>
        public SiteInfo SiteInfo
        {
            get
            {
                AssertSiteInfoInitialized();
                return _SiteInfo;
            }
        }

        /// <summary>
        /// Gets the currrent user's account information.
        /// </summary>
        public AccountInfo AccountInfo
        {
            get
            {
                if (_AccountInfo == null)
                    throw new InvalidOperationException(
                        "Site.RefreshUserInfoAsync should be successfully invoked before performing the operation.");
                return _AccountInfo;
            }
        }

        /// <summary>
        /// Gets a collection of namespaces used on this MediaWiki site.
        /// </summary>
        public NamespaceCollection Namespaces
        {
            get
            {
                AssertSiteInfoInitialized();
                return _Namespaces;
            }
        }

        /// <summary>
        /// Gets the interwiki map information on this wiki site.
        /// </summary>
        public InterwikiMap InterwikiMap
        {
            get
            {
                AssertSiteInfoInitialized();
                return _InterwikiMap;
            }
        }

        /// <summary>
        /// Gets a collection of installed extensions on this MediaWiki site.
        /// </summary>
        public ExtensionCollection Extensions
        {
            get
            {
                AssertSiteInfoInitialized();
                return _Extensions;
            }
        }

        /// <summary>
        /// The endpoint URL for MediaWiki API.
        /// </summary>
        public string ApiEndpoint => options.ApiEndpoint;

        /// <summary>
        /// Gets the default result limit per page for current user.
        /// </summary>
        /// <value>This value is 500 for user, and 5000 for bots.</value>
        // Use 500 for default. This value will be updated along with UserInfo.
        internal int ListingPagingSize { get; private set; } = 500;

        /// <summary>
        /// Gets a list of titles of disambiguation templates. The default DAB template title
        /// will NOT be included in the list.
        /// </summary>
        internal readonly AsyncLazy<ICollection<string>> DisambiguationTemplatesAsync;

        #region Basic API

        private static readonly IList<KeyValuePair<string, string>> accountAssertionUser = new[]
            {new KeyValuePair<string, string>("assert", "user")};

        private static readonly IList<KeyValuePair<string, string>> accountAssertionBot = new[]
            {new KeyValuePair<string, string>("assert", "bot")};


        /// <summary>
        /// Invokes API and get JSON result.
        /// </summary>
        /// <exception cref="InvalidActionException">Specified action is not supported.</exception>
        /// <exception cref="OperationCanceledException">The operation has been cancelled via <paramref name="cancellationToken"/>.</exception>
        /// <exception cref="UnauthorizedOperationException">Permission denied.</exception>
        /// <exception cref="OperationFailedException">There's "error" node in returned JSON.</exception>
        /// <remarks>The request is sent via HTTP POST.</remarks>
        [Obsolete("Please use WikiSite.GetJsonAsync method.")]
        public Task<JToken> PostValuesAsync(IEnumerable<KeyValuePair<string, string>> queryParams,
            CancellationToken cancellationToken)
            => GetJsonAsync(new WikiFormRequestMessage(queryParams), false, cancellationToken);

        /// <summary>
        /// Invokes API and get JSON result.
        /// </summary>
        /// <exception cref="InvalidActionException">Specified action is not supported.</exception>
        /// <exception cref="OperationCanceledException">The operation has been cancelled via <paramref name="cancellationToken"/>.</exception>
        /// <exception cref="UnauthorizedOperationException">Permission denied.</exception>
        /// <exception cref="OperationFailedException">There's "error" node in returned JSON.</exception>
        /// <remarks>The request is sent via HTTP POST.</remarks>
        [Obsolete("Please use WikiSite.GetJsonAsync method.")]
        public Task<JToken> PostValuesAsync(IEnumerable<KeyValuePair<string, string>> queryParams,
            bool supressAccountAssertion, CancellationToken cancellationToken)
        {
            return GetJsonAsync(new WikiFormRequestMessage(new WikiFormRequestMessage(queryParams)),
                supressAccountAssertion, cancellationToken);
        }

        public Task<JToken> GetJsonAsync(WikiRequestMessage message, CancellationToken cancellationToken)
        {
            return GetJsonAsync(message, false, cancellationToken);
        }

        public async Task<JToken> GetJsonAsync(WikiRequestMessage message, bool supressAccountAssertion,
            CancellationToken cancellationToken)
        {
            var form = message as WikiFormRequestMessage;
            var localRequest = message;
            RETRY:
            if (form != null)
            {
                // Apply tokens
                var overridenFields = new List<KeyValuePair<string, object>>();
                foreach (var tokenField in form.Fields.Where(p => p.Value is WikiSiteToken))
                {
                    overridenFields.Add(new KeyValuePair<string, object>(tokenField.Key,
                        await tokensManager.GetTokenAsync(((WikiSiteToken) tokenField.Value).Type,
                            false, cancellationToken)));
                }
                // Apply account assertions
                if (!supressAccountAssertion && _AccountInfo != null)
                {
                    if ((options.AccountAssertion & AccountAssertionBehavior.AssertBot) ==
                        AccountAssertionBehavior.AssertBot && _AccountInfo.IsBot)
                        overridenFields.Add(new KeyValuePair<string, object>("assert", "bot"));
                    else if ((options.AccountAssertion & AccountAssertionBehavior.AssertUser) ==
                             AccountAssertionBehavior.AssertUser && _AccountInfo.IsUser)
                        overridenFields.Add(new KeyValuePair<string, object>("assert", "user"));
                }
                if (overridenFields.Count > 0)
                    localRequest = new WikiFormRequestMessage(form.Id, form, overridenFields, false);
            }
            try
            {
                return await WikiClient.GetJsonAsync(options.ApiEndpoint, localRequest, cancellationToken);
            }
            catch (AccountAssertionFailureException)
            {
                if (AccountAssertionFailureHandler != null)
                {
                    // ISSUE Relogin might be called nultiple times.
                    if (reLoginTask == null)
                    {
                        Logger.LogWarning("Account assertion failed. Try to relogin: {Request}.", message);
                        Volatile.Write(ref reLoginTask, Relogin());
                    }
                    else
                    {
                        Logger.LogWarning("Account assertion failed. Waiting for relongin: {Request}.", message);
                    }
                    var result = await reLoginTask;
                    if (result) goto RETRY;
                }
                throw;
            }
        }

        /// <summary>
        /// Invoke API and get JSON result.
        /// </summary>
        /// <param name="queryParams">An object whose proeprty-value pairs will be converted into key-value pairs and sent.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <exception cref="InvalidActionException">Specified action is not supported.</exception>
        /// <exception cref="OperationCanceledException">The operation has been cancelled via <paramref name="cancellationToken"/>.</exception>
        /// <exception cref="OperationFailedException">There's "error" node in returned JSON.</exception>
        [Obsolete("Please use WikiSite.GetJsonAsync method.")]
        public Task<JToken> PostValuesAsync(object queryParams, CancellationToken cancellationToken)
            => GetJsonAsync(new WikiFormRequestMessage(queryParams), false, cancellationToken);

        /// <summary>
        /// Invoke API and get JSON result.
        /// </summary>
        /// <param name="queryParams">An object whose proeprty-value pairs will be converted into key-value pairs and sent.</param>
        /// <param name="supressAccountAssertion">Whether to temporarily disable account assertion as set in <see cref="SiteOptions.AccountAssertion"/>.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <exception cref="InvalidActionException">Specified action is not supported.</exception>
        /// <exception cref="OperationCanceledException">The operation has been cancelled via <paramref name="cancellationToken"/>.</exception>
        /// <exception cref="OperationFailedException">There's "error" node in returned JSON.</exception>
        [Obsolete("Please use WikiSite.GetJsonAsync method.")]
        public Task<JToken> PostValuesAsync(object queryParams, bool supressAccountAssertion,
            CancellationToken cancellationToken)
        {
            if (queryParams == null) throw new ArgumentNullException(nameof(queryParams));
            return GetJsonAsync(new WikiFormRequestMessage(Utility.ToWikiStringValuePairs(queryParams)),
                supressAccountAssertion,
                cancellationToken);
        }

        #endregion

        #region Tokens

        /// <summary>
        /// Request tokens for operations.
        /// </summary>
        /// <param name="tokenTypes">The names of token. Names should be as accurate as possible (E.g. use "edit" instead of "csrf").</param>
        /// <param name="forceRefetch">Whether to fetch token from server, regardless of the cache.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <exception cref="InvalidOperationException">One or more specified token types cannot be recognized.</exception>
        /// <remarks>
        /// <para>This method is thread-safe.</para>
        /// <para>See https://www.mediawiki.org/wiki/API:Tokens .</para>
        /// </remarks>
        public Task<IDictionary<string, string>> GetTokensAsync(IEnumerable<string> tokenTypes, bool forceRefetch,
            CancellationToken cancellationToken)
        {
            return tokensManager.GetTokensAsync(tokenTypes, forceRefetch, cancellationToken);
        }

        /// <summary>
        /// Request a token for operation.
        /// </summary>
        /// <param name="tokenType">The name of token.</param>
        /// <exception cref="InvalidOperationException">Specified token type cannot be recognized.</exception>
        /// <remarks>See https://www.mediawiki.org/wiki/API:Tokens .</remarks>
        public Task<string> GetTokenAsync(string tokenType)
        {
            return GetTokenAsync(tokenType, false, CancellationToken.None);
        }

        /// <summary>
        /// Request a token for operation.
        /// </summary>
        /// <param name="tokenType">The name of token.</param>
        /// <param name="forceRefetch">Whether to fetch token from server, regardless of the cache.</param>
        /// <remarks>See https://www.mediawiki.org/wiki/API:Tokens .</remarks>
        public Task<string> GetTokenAsync(string tokenType, bool forceRefetch)
        {
            return GetTokenAsync(tokenType, forceRefetch, CancellationToken.None);
        }

        /// <summary>
        /// Request a token for operation.
        /// </summary>
        /// <param name="tokenType">The name of token.</param>
        /// <remarks>See https://www.mediawiki.org/wiki/API:Tokens .</remarks>
        public Task<string> GetTokenAsync(string tokenType, CancellationToken cancellationToken)
        {
            return GetTokenAsync(tokenType, false, cancellationToken);
        }

        /// <summary>
        /// Request a token for operation.
        /// </summary>
        /// <param name="tokenType">The name of token.</param>
        /// <param name="forceRefetch">Whether to fetch token from server, regardless of the cache.</param>
        /// <remarks>See https://www.mediawiki.org/wiki/API:Tokens .</remarks>
        public Task<string> GetTokenAsync(string tokenType, bool forceRefetch, CancellationToken cancellationToken)
        {
            return tokensManager.GetTokenAsync(tokenType, forceRefetch, cancellationToken);
        }

        #endregion

        #region Authentication

        private int isLoggingInOut = 0;

        /// <summary>
        /// Logins into the wiki site.
        /// </summary>
        /// <param name="userName">User name of the account.</param>
        /// <param name="password">Password of the account.</param>
        /// <exception cref="InvalidOperationException">Attempt to login/logout concurrently.</exception>
        /// <exception cref="OperationFailedException">Canoot login with the specified credential.</exception>
        /// <exception cref="ArgumentNullException">Either <paramref name="userName"/> or <paramref name="password"/> is <c>null</c> or empty.</exception>
        /// <remarks>This operation will refresh <see cref="AccountInfo"/>.</remarks>
        public Task LoginAsync(string userName, string password)
        {
            return LoginAsync(userName, password, null, CancellationToken.None);
        }

        /// <summary>
        /// Logins into the wiki site.
        /// </summary>
        /// <param name="userName">User name of the account.</param>
        /// <param name="password">Password of the account.</param>
        /// <param name="domain">Domain name. <c>null</c> is usually a good choice.</param>
        /// <exception cref="InvalidOperationException">Attempt to login/logout concurrently.</exception>
        /// <exception cref="OperationFailedException">Canoot login with the specified credential.</exception>
        /// <exception cref="ArgumentNullException">Either <paramref name="userName"/> or <paramref name="password"/> is <c>null</c> or empty.</exception>
        /// <remarks>This operation will refresh <see cref="AccountInfo"/>.</remarks>
        public Task LoginAsync(string userName, string password, string domain)
        {
            return LoginAsync(userName, password, domain, CancellationToken.None);
        }

        /// <summary>
        /// Logins into the wiki site.
        /// </summary>
        /// <param name="userName">User name of the account.</param>
        /// <param name="password">Password of the account.</param>
        /// <param name="domain">Domain name. <c>null</c> is usually a good choice.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <exception cref="InvalidOperationException">Attempt to login/logout concurrently.</exception>
        /// <exception cref="OperationFailedException">Canoot login with the specified credential.</exception>
        /// <exception cref="ArgumentNullException">Either <paramref name="userName"/> or <paramref name="password"/> is <c>null</c> or empty.</exception>
        /// <remarks>This operation will refresh <see cref="AccountInfo"/>.</remarks>
        public async Task LoginAsync(string userName, string password, string domain,
            CancellationToken cancellationToken)
        {
            // Note: this method may be invoked BEFORE the initialization of _SiteInfo.
            if (string.IsNullOrEmpty(userName)) throw new ArgumentNullException(nameof(userName));
            if (string.IsNullOrEmpty(password)) throw new ArgumentNullException(nameof(password));
            if (Interlocked.Exchange(ref isLoggingInOut, 1) != 0)
                throw new InvalidOperationException("Cannot login/logout concurrently.");
            try
            {
                string token = null;
                // If _SiteInfo is null, it indicates options.ExplicitInfoRefresh must be true.
                Debug.Assert(options.ExplicitInfoRefresh || _SiteInfo != null);
                // For MedaiWiki 1.27+
                if (!options.ExplicitInfoRefresh && _SiteInfo.Version >= new Version("1.27"))
                    token = await GetTokenAsync("login", true, cancellationToken);
                // For MedaiWiki < 1.27, We'll have to request twice.
                // If options.ExplicitInfoRefresh is true, we just treat it the same as MedaiWiki < 1.27,
                //  because any "query" operation might raise readapidenied error.
                RETRY:
                var jobj = await GetJsonAsync(new WikiFormRequestMessage(new
                {
                    action = "login",
                    lgname = userName,
                    lgpassword = password,
                    lgtoken = token,
                    lgdomain = domain,
                }), true, cancellationToken);
                var result = (string) jobj["login"]["result"];
                string message = null;
                switch (result)
                {
                    case "Success":
                        tokensManager.ClearCache();
                        await RefreshAccountInfoAsync();
                        Debug.Assert(AccountInfo.IsUser,
                            "API result indicates the login is successful, but you are not currently in \"user\" group. Are you logging out on the other Site instance with the same API endpoint and the same WikiClient?");
                        return;
                    case "Aborted":
                        message =
                            "The login using the main account password (rather than a bot password) cannot proceed because user interaction is required. The clientlogin action should be used instead.";
                        break;
                    case "Throttled":
                        var time = (int) jobj["login"]["wait"];
                        Logger.LogWarning("{Site} login throttled: {Time}sec.", ToString(), time);
                        await Task.Delay(TimeSpan.FromSeconds(time), cancellationToken);
                        goto RETRY;
                    case "NeedToken":
                        token = (string) jobj["login"]["token"];
                        goto RETRY;
                    case "WrongToken": // We should have got correct token.
                        throw new UnexpectedDataException($"Unexpected login result: {result} .");
                }
                message = (string) jobj["login"]["reason"] ?? message;
                throw new OperationFailedException(result, message);
            }
            finally
            {
                Interlocked.Exchange(ref isLoggingInOut, 0);
            }
        }

        /// <summary>
        /// Logouts from the wiki site.
        /// </summary>
        /// <remarks>This operation will refresh <see cref="AccountInfo"/>,
        /// unless <see cref="SiteOptions.ExplicitInfoRefresh"/> is <c>true</c> when initializing
        /// the instance. In the latter case, <see cref="AccountInfo"/> will be invalidated,
        /// and any attempt to read the property will raise <see cref="InvalidOperationException"/>
        /// until the next successful login.</remarks>
        /// <exception cref="InvalidOperationException">Attempt to login/logout concurrently.</exception>
        public async Task LogoutAsync()
        {
            if (Interlocked.Exchange(ref isLoggingInOut, 1) != 0)
                throw new InvalidOperationException("Cannot login/logout concurrently.");
            try
            {
                var jobj = await GetJsonAsync(new WikiFormRequestMessage(new
                {
                    action = "logout",
                }), true, CancellationToken.None);
                tokensManager.ClearCache();
                if (options.ExplicitInfoRefresh)
                    _AccountInfo = null;
                else
                    await RefreshAccountInfoAsync();
            }
            finally
            {
                Interlocked.Exchange(ref isLoggingInOut, 0);
            }
        }

        private Task<bool> reLoginTask;
        private ILoggerFactory _LoggerFactory;

        private async Task<bool> Relogin()
        {
            Debug.Assert(AccountAssertionFailureHandler != null);
            var result = await AccountAssertionFailureHandler.Login(this);
            reLoginTask = null;
            return result;
        }

        #endregion

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        /// <returns>
        /// 表示当前对象的字符串。
        /// </returns>
        public override string ToString()
        {
            return string.IsNullOrEmpty(SiteInfo.SiteName) ? options.ApiEndpoint : SiteInfo.SiteName;
        }

        protected internal ILogger Logger { get; private set; } = NullLogger.Instance;

        /// <inheritdoc />
        public ILoggerFactory LoggerFactory
        {
            get => _LoggerFactory;
            set => Logger = Utility.SetLoggerFactory(ref _LoggerFactory, value, GetType());
        }
    }

}

