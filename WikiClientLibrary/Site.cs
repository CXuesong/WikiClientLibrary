using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using System.Security;
using System.Threading;

namespace WikiClientLibrary
{
    /// <summary>
    /// Represents a MediaWiki site.
    /// </summary>
    public class Site
    {

        private readonly SiteOptions options;

        #region Services

        public WikiClientBase WikiClient { get; }

        public ILogger Logger { get; set; }

        /// <summary>
        /// A handler used to re-login when account assertion fails.
        /// </summary>
        public IAccountAssertionFailureHandler AccountAssertionFailureHandler { get; set; }

        #endregion

        /// <summary>
        /// Initialize a <see cref="Site"/> instance with the given API Endpoint URL.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="wikiClient"/> or <paramref name="apiEndpoint"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="apiEndpoint"/> is invalid.</exception>
        /// <exception cref="UnauthorizedOperationException">Cannot access query API module due to target site permission settings. You may take a look at <see cref="SiteOptions.ExplicitInfoRefresh"/>.</exception>
        public static Task<Site> CreateAsync(WikiClientBase wikiClient, string apiEndpoint)
        {
            if (wikiClient == null) throw new ArgumentNullException(nameof(wikiClient));
            if (apiEndpoint == null) throw new ArgumentNullException(nameof(apiEndpoint));
            return CreateAsync(wikiClient, new SiteOptions(apiEndpoint));
        }

        /// <summary>
        /// Initialize a <see cref="Site"/> instance with the specified settings.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="wikiClient"/> or <paramref name="options"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">One or more settings in <paramref name="options"/> is invalid.</exception>
        /// <exception cref="UnauthorizedOperationException">Cannot access query API module due to target site permission settings. You may take a look at <see cref="SiteOptions.ExplicitInfoRefresh"/>.</exception>
        public static async Task<Site> CreateAsync(WikiClientBase wikiClient, SiteOptions options)
        {
            if (wikiClient == null) throw new ArgumentNullException(nameof(wikiClient));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrEmpty(options.ApiEndpoint))
                throw new ArgumentException("Invalid API endpoint url.", nameof(options));
            var site = new Site(wikiClient, options);
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
            return MediaWikiUtility.SearchApiEndpointAsync(client, urlExpression);
        }

        protected Site(WikiClientBase wikiClient, SiteOptions options)
        {
            // Perform basic checks.
            Debug.Assert(wikiClient != null);
            Debug.Assert(options != null);
            WikiClient = wikiClient;
            this.options = options.Clone();
            DisambiguationTemplatesAsync = new AsyncLazy<ICollection<string>>(async () =>
            {
                if (this.options.DisambiguationTemplates == null)
                {
                    var dabPages = await RequestHelper
                        .EnumLinksAsync(this, "MediaWiki:Disambiguationspage", new[] { BuiltInNamespaces.Template })
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
            var jobj = await PostValuesAsync(new
            {
                action = "query",
                meta = "siteinfo",
                siprop = "general|namespaces|namespacealiases|interwikimap|extensions"
            }, true, CancellationToken.None);
            var qg = (JObject)jobj["query"]["general"];
            var ns = (JObject)jobj["query"]["namespaces"];
            var aliases = (JArray)jobj["query"]["namespacealiases"];
            var interwiki = (JArray)jobj["query"]["interwikimap"];
            var extensions = (JArray)jobj["query"]["extensions"];
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
            var jobj = await PostValuesAsync(new
            {
                action = "query",
                meta = "userinfo",
                uiprop = "blockinfo|groups|hasmsg|rights"
            }, true, CancellationToken.None);
            _AccountInfo = ((JObject)jobj["query"]["userinfo"]).ToObject<AccountInfo>(Utility.WikiJsonSerializer);
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
        public Task<JToken> PostValuesAsync(IEnumerable<KeyValuePair<string, string>> queryParams,
                CancellationToken cancellationToken)
            => PostValuesAsync(queryParams, false, cancellationToken);

        /// <summary>
        /// Invokes API and get JSON result.
        /// </summary>
        /// <exception cref="InvalidActionException">Specified action is not supported.</exception>
        /// <exception cref="OperationCanceledException">The operation has been cancelled via <paramref name="cancellationToken"/>.</exception>
        /// <exception cref="UnauthorizedOperationException">Permission denied.</exception>
        /// <exception cref="OperationFailedException">There's "error" node in returned JSON.</exception>
        /// <remarks>The request is sent via HTTP POST.</remarks>
        public async Task<JToken> PostValuesAsync(IEnumerable<KeyValuePair<string, string>> queryParams, bool supressAccountAssertion,
            CancellationToken cancellationToken)
        {
            var queryParams1 = queryParams as ICollection<KeyValuePair<string, string>> ?? queryParams.ToArray();
            RETRY:
            IEnumerable<KeyValuePair<string, string>> pa = queryParams1;
            if (!supressAccountAssertion && _AccountInfo != null)
            {
                if ((options.AccountAssertion & AccountAssertionBehavior.AssertBot) ==
                    AccountAssertionBehavior.AssertBot && _AccountInfo.IsBot)
                    pa = pa.Concat(accountAssertionBot);
                else if ((options.AccountAssertion & AccountAssertionBehavior.AssertUser) ==
                         AccountAssertionBehavior.AssertUser && _AccountInfo.IsUser)
                    pa = pa.Concat(accountAssertionUser);
            }
            try
            {
                return await WikiClient.GetJsonAsync(options.ApiEndpoint, pa, cancellationToken);
            }
            catch (AccountAssertionFailureException)
            {
                if (AccountAssertionFailureHandler != null)
                {
                    // TODO Not thread safe?
                    if (reLoginTask == null)
                    {
                        reLoginTask = Relogin();
                        Logger?.Warn(this, "Account assertion failed. Try to handle this.");
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
        public Task<JToken> PostValuesAsync(object queryParams, CancellationToken cancellationToken)
            => PostValuesAsync(queryParams, false, cancellationToken);

        /// <summary>
        /// Invoke API and get JSON result.
        /// </summary>
        /// <param name="queryParams">An object whose proeprty-value pairs will be converted into key-value pairs and sent.</param>
        /// <param name="supressAccountAssertion">Whether to temporarily disable account assertion as set in <see cref="SiteOptions.AccountAssertion"/>.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <exception cref="InvalidActionException">Specified action is not supported.</exception>
        /// <exception cref="OperationCanceledException">The operation has been cancelled via <paramref name="cancellationToken"/>.</exception>
        /// <exception cref="OperationFailedException">There's "error" node in returned JSON.</exception>
        public Task<JToken> PostValuesAsync(object queryParams, bool supressAccountAssertion, CancellationToken cancellationToken)
        {
            return PostValuesAsync(Utility.ToWikiStringValuePairs(queryParams), supressAccountAssertion, cancellationToken);
        }

        // No, we cannot guarantee the returned value is JSON, so this function is internal.
        // It depends on caller's conscious.
        internal Task<JToken> PostContentAsync(HttpContent postContent, CancellationToken cancellationToken)
        {
            return WikiClient.GetJsonAsync(options.ApiEndpoint, postContent, cancellationToken);
        }

        #endregion

        #region Tokens

        /// <summary>
        /// Tokens that have been merged into CSRF token since MediaWiki 1.24 .
        /// </summary>
        private static readonly string[] CsrfTokens =
        {
            "edit", "delete", "protect", "move", "block", "unblock", "email",
            "import"
        };

        private readonly Dictionary<string, string> _TokensCache = new Dictionary<string, string>();

        /// <summary>
        /// Fetch tokens. (MediaWiki 1.24)
        /// </summary>
        /// <param name="tokenTypeExpr">Token types, joined by | .</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        private async Task<JObject> FetchTokensAsync2(string tokenTypeExpr, CancellationToken cancellationToken)
        {
            var jobj = await PostValuesAsync(new
            {
                action = "query",
                meta = "tokens",
                type = tokenTypeExpr,
            }, true, cancellationToken);
            var warnings = jobj["warnings"]?["tokens"];
            if (warnings != null)
            {
                // "*": "Unrecognized value for parameter 'type': xxxx"
                var warn = (string)warnings["*"];
                if (warn != null && warn.Contains("Unrecognized value") && warn.Contains("type"))
                    throw new ArgumentException(warn, nameof(tokenTypeExpr));
                throw new OperationFailedException(warnings.ToString());
            }
            return (JObject)jobj["query"]["tokens"];
        }

        /// <summary>
        /// Fetch tokens. (MediaWiki &lt; 1.24)
        /// </summary>
        /// <param name="tokenTypeExpr">Token types, joined by | .</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        private async Task<JObject> FetchTokensAsync(string tokenTypeExpr, CancellationToken cancellationToken)
        {
            Debug.Assert(!tokenTypeExpr.Contains("patrol"));
            var jobj = await PostValuesAsync(new
            {
                action = "query",
                prop = "info",
                titles = "Dummy Title",
                intoken = tokenTypeExpr,
            }, true, cancellationToken);
            var page = (JObject)((JProperty)jobj["query"]["pages"].First).Value;
            return new JObject(page.Properties().Where(p => p.Name.EndsWith("token")));
        }

        /// <summary>
        /// Request tokens for operations.
        /// </summary>
        /// <param name="tokenTypes">The names of token.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <remarks>See https://www.mediawiki.org/wiki/API:Tokens .</remarks>
        public Task<IDictionary<string, string>> GetTokensAsync(IEnumerable<string> tokenTypes, CancellationToken cancellationToken)
        {
            return GetTokensAsync(tokenTypes, false, cancellationToken);
        }

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
        public async Task<IDictionary<string, string>> GetTokensAsync(IEnumerable<string> tokenTypes, bool forceRefetch, CancellationToken cancellationToken)
        {
            // TODO wait for other threads to fetch a token, instead of fetching whenever it's not
            // available, even if there's already another thread requesting it.
            if (tokenTypes == null) throw new ArgumentNullException(nameof(tokenTypes));
            var tokenTypesList = tokenTypes as IReadOnlyList<string> ?? tokenTypes.ToList();
            List<string> pendingtokens;
            lock (_TokensCache)
            {
                pendingtokens = tokenTypesList.Where(tt => forceRefetch || !_TokensCache.ContainsKey(tt)).ToList();
            }
            JObject fetchedTokens = null;
            if (SiteInfo.Version < new Version("1.24"))
            {
                /*
                 Patrol was added in v1.14.
                 Until v1.16, the patrol token is same as the edit token.
                 For v1.17-19, the patrol token must be obtained from the query
                 list recentchanges.
                 */
                var needPatrolFromRC = false;
                // Check whether we need a patrol token.
                if (SiteInfo.Version < new Version("1.20"))
                    needPatrolFromRC = pendingtokens.Remove("patrol");
                if (needPatrolFromRC)
                {
                    string patrolToken;
                    lock (_TokensCache)
                        if (!_TokensCache.TryGetValue("patrol", out patrolToken)) patrolToken = null;
                    if (patrolToken == null)
                    {
                        if (SiteInfo.Version < new Version("1.17"))
                        {
                            patrolToken = await GetTokenAsync("edit");
                        }
                        else
                        {
                            var jobj = await PostValuesAsync(new
                            {
                                action = "query",
                                meta = "recentchanges",
                                rctoken = "patrol",
                                rclimit = 1
                            }, cancellationToken);
                            patrolToken = (string)jobj["query"]["recentchanges"]["patroltoken"];
                        }
                        lock (_TokensCache)
                            _TokensCache["patrol"] = patrolToken;
                    }
                }
                if (pendingtokens.Count > 0)
                    fetchedTokens = await FetchTokensAsync(string.Join("|", pendingtokens), cancellationToken);
            }
            else
            {
                // Use csrf token if possible.
                if (!pendingtokens.Contains("csrf"))
                {
                    var needCsrf = false;
                    foreach (var t in CsrfTokens)
                    {
                        if (pendingtokens.Remove(t)) needCsrf = true;
                    }
                    if (needCsrf) pendingtokens.Add("csrf");
                }
                if (pendingtokens.Count > 0)
                {
                    fetchedTokens = await FetchTokensAsync2(string.Join("|", pendingtokens), cancellationToken);
                    var csrf = (string)fetchedTokens["csrftoken"];
                    if (csrf != null)
                    {
                        lock (_TokensCache)
                            foreach (var t in CsrfTokens) _TokensCache[t] = csrf;
                    }
                }
            }
            // Put tokens into cache first.
            if (fetchedTokens != null)
            {
                foreach (var p in fetchedTokens.Properties())
                {
                    // Remove "token" in the result
                    var tokenName = p.Name.EndsWith("token")
                        ? p.Name.Substring(0, p.Name.Length - 5)
                        : p.Name;
                    lock (_TokensCache)
                    {
                        _TokensCache[tokenName] = (string)p.Value;
                    }
                    pendingtokens.Remove(tokenName);
                }
                if (pendingtokens.Count > 0)
                {
                    throw new InvalidOperationException(
                        "Unrecognized token(s): " + string.Join(", ", pendingtokens) + ".");
                }
            }
            // Then return.
            lock (_TokensCache)
                return tokenTypesList.ToDictionary(t => t, t => _TokensCache[t]);
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
            return GetTokenAsync(tokenType, forceRefetch, new CancellationToken());
        }

        /// <summary>
        /// Request a token for operation.
        /// </summary>
        /// <param name="tokenType">The name of token.</param>
        /// <param name="forceRefetch">Whether to fetch token from server, regardless of the cache.</param>
        /// <remarks>See https://www.mediawiki.org/wiki/API:Tokens .</remarks>
        public async Task<string> GetTokenAsync(string tokenType, bool forceRefetch, CancellationToken cancellationToken)
        {
            if (tokenType == null) throw new ArgumentNullException(nameof(tokenType));
            if (tokenType.Contains("|"))
                throw new ArgumentException("Pipe character in token type name.", nameof(tokenType));
            var dict = await GetTokensAsync(new[] { tokenType }, forceRefetch, cancellationToken);
            return dict.Values.Single();
        }

        #endregion

        #region Authentication

        /// <summary>
        /// Logins into the wiki site.
        /// </summary>
        /// <param name="userName">User name of the account.</param>
        /// <param name="password">Password of the account.</param>
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
        /// <exception cref="ArgumentNullException">Either <paramref name="userName"/> or <paramref name="password"/> is <c>null</c> or empty.</exception>
        /// <remarks>This operation will refresh <see cref="AccountInfo"/>.</remarks>
        public Task LoginAsync(string userName, string password, string domain)
        {
            return LoginAsync(userName, password, domain, new CancellationToken());
        }

        /// <summary>
        /// Logins into the wiki site.
        /// </summary>
        /// <param name="userName">User name of the account.</param>
        /// <param name="password">Password of the account.</param>
        /// <param name="domain">Domain name. <c>null</c> is usually a good choice.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <exception cref="OperationFailedException">Canoot login with the specified credential.</exception>
        /// <exception cref="ArgumentNullException">Either <paramref name="userName"/> or <paramref name="password"/> is <c>null</c> or empty.</exception>
        /// <remarks>This operation will refresh <see cref="AccountInfo"/>.</remarks>
        public async Task LoginAsync(string userName, string password, string domain, CancellationToken cancellationToken)
        {
            // Note: this method may be invoked BEFORE the initialization of _SiteInfo.
            if (string.IsNullOrEmpty(userName)) throw new ArgumentNullException(nameof(userName));
            if (string.IsNullOrEmpty(password)) throw new ArgumentNullException(nameof(password));
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
            var jobj = await PostValuesAsync(new
            {
                action = "login",
                lgname = userName,
                lgpassword = password,
                lgtoken = token,
                lgdomain = domain,
            }, true, cancellationToken);
            var result = (string)jobj["login"]["result"];
            string message = null;
            switch (result)
            {
                case "Success":
                    _TokensCache.Clear();
                    await RefreshAccountInfoAsync();
                    Debug.Assert(AccountInfo.IsUser);
                    return;
                case "Aborted":
                    message =
                        "The login using the main account password (rather than a bot password) cannot proceed because user interaction is required. The clientlogin action should be used instead.";
                    break;
                case "Throttled":
                    var time = (int)jobj["login"]["wait"];
                    Logger?.Warn(this, $"Throttled: {time}sec.");
                    await Task.Delay(TimeSpan.FromSeconds(time), cancellationToken);
                    goto RETRY;
                case "NeedToken":
                    token = (string)jobj["login"]["token"];
                    goto RETRY;
                case "WrongToken": // We should have got correct token.
                    throw new UnexpectedDataException($"Unexpected login result: {result} .");
            }
            message = (string)jobj["login"]["reason"] ?? message;
            throw new OperationFailedException(result, message);
        }

        /// <summary>
        /// Logouts from the wiki site.
        /// </summary>
        /// <remarks>This operation will refresh <see cref="AccountInfo"/>,
        /// unless <see cref="SiteOptions.ExplicitInfoRefresh"/> is <c>true</c> when initializing
        /// the instance. In the latter case, <see cref="AccountInfo"/> will be invalidated,
        /// and any attempt to read the property will raise <see cref="InvalidOperationException"/>
        /// until the next successful login.</remarks>
        public async Task LogoutAsync()
        {
            var jobj = await PostValuesAsync(new
            {
                action = "logout",
            }, true, CancellationToken.None);
            _TokensCache.Clear();
            if (options.ExplicitInfoRefresh)
                _AccountInfo = null;
            else
                await RefreshAccountInfoAsync();
        }

        private volatile Task<bool> reLoginTask;

        private async Task<bool> Relogin()
        {
            Debug.Assert(AccountAssertionFailureHandler != null);
            var result = await AccountAssertionFailureHandler.Login(this);
            reLoginTask = null;
            return result;
        }

        #endregion

        #region Query
        private readonly AsyncReaderWriterLock cachedMessagesLock = new AsyncReaderWriterLock();
        private readonly IDictionary<string, string> _CachedMessages = new Dictionary<string, string>();

        private async Task<JArray> FetchMessagesAsync(string messagesExpr, CancellationToken cancellationToken)
        {
            var jresult = await PostValuesAsync(new
            {
                action = "query",
                meta = "allmessages",
                ammessages = messagesExpr,
            }, cancellationToken);
            return (JArray)jresult["query"]["allmessages"];
            //return jresult.ToDictionary(m => , m => (string) m["*"]);
        }

        /// <summary>
        /// Get the content of some or all MediaWiki interface messages.
        /// </summary>
        /// <param name="messages">A sequence of message names.</param>
        /// <returns>
        /// A dictionary of message name - message content pairs.
        /// If some messages cannot be found, the corresponding value will be <c>null</c>.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="messages"/> contains <c>null</c> item.
        /// OR one of the <paramref name="messages"/> contains pipe character (|).
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Trying to fetch all the messages with "*" input.
        /// </exception>
        public Task<IDictionary<string, string>> GetMessagesAsync(IEnumerable<string> messages)
        {
            return GetMessagesAsync(messages, new CancellationToken());
        }

        /// <summary>
        /// Get the content of some or all MediaWiki interface messages.
        /// </summary>
        /// <param name="messages">A sequence of message names.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <returns>
        /// A dictionary of message name - message content pairs.
        /// If some messages cannot be found, the corresponding value will be <c>null</c>.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="messages"/> contains <c>null</c> item.
        /// OR one of the <paramref name="messages"/> contains pipe character (|).
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Trying to fetch all the messages with "*" input.
        /// </exception>
        public async Task<IDictionary<string, string>> GetMessagesAsync(IEnumerable<string> messages,
            CancellationToken cancellationToken)
        {
            if (messages == null) throw new ArgumentNullException(nameof(messages));
            cancellationToken.ThrowIfCancellationRequested();
            var impending = new List<string>();
            var result = new Dictionary<string, string>();
            using (await cachedMessagesLock.ReaderLockAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var m in messages)
                {
                    if (m == null) throw new ArgumentException("The sequence contains null item.", nameof(messages));
                    if (m.Contains("|"))
                        throw new ArgumentException($"The message name \"{m}\" contains pipe character.",
                            nameof(messages));
                    if (m == "*") throw new InvalidOperationException("Getting all the messages is deprecated.");
                    string content;
                    if (_CachedMessages.TryGetValue(m.ToLowerInvariant(), out content))
                        result[m] = content;
                    else
                        impending.Add(m);
                }
            }
            if (impending.Count > 0)
            {
                using (await cachedMessagesLock.WriterLockAsync(cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var jr = await FetchMessagesAsync(string.Join("|", impending), cancellationToken);
                    foreach (var entry in jr)
                    {
                        var name = (string)entry["name"];
                        //var nname = (string)entry["normalizedname"];
                        // for Wikia, there's no normalizedname
                        var message = (string)entry["*"];
                        //var missing = entry["missing"] != null;       message will be null
                        result[name] = message;
                        _CachedMessages[name] = message;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Get the content of MediaWiki interface message.
        /// </summary>
        /// <param name="message">The message name.</param>
        /// <returns>
        /// The message content. OR <c>null</c> if the messages cannot be found.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="message"/> contains pipe character (|).
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Trying to fetch all the messages with "*" input.
        /// </exception>
        public Task<string> GetMessageAsync(string message)
        {
            return GetMessageAsync(message, new CancellationToken());
        }

        /// <summary>
        /// Get the content of MediaWiki interface message.
        /// </summary>
        /// <param name="message">The message name.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <returns>
        /// The message content. OR <c>null</c> if the messages cannot be found.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="message"/> contains pipe character (|).
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Trying to fetch all the messages with "*" input.
        /// </exception>
        public async Task<string> GetMessageAsync(string message, CancellationToken cancellationToken)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            var result = await GetMessagesAsync(new[] { message }, cancellationToken);
            return result.Values.FirstOrDefault();
        }

        /// <summary>
        /// Gets the statistical information of the MediaWiki site.
        /// </summary>
        public Task<SiteStatistics> GetStatisticsAsync()
        {
            return GetStatisticsAsync(new CancellationToken());
        }

        /// <summary>
        /// Gets the statistical information of the MediaWiki site.
        /// </summary>
        public async Task<SiteStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
        {
            var jobj = await PostValuesAsync(new
            {
                action = "query",
                meta = "siteinfo",
                siprop = "statistics",
            }, cancellationToken);
            var jstat = (JObject)jobj["query"]?["statistics"];
            if (jstat == null) throw new UnexpectedDataException();
            var parsed = jstat.ToObject<SiteStatistics>();
            return parsed;
        }

        /// <summary>
        /// Performs an opensearch and get results, often used for search box suggestions.
        /// (MediaWiki 1.25 or OpenSearch extension)
        /// </summary>
        /// <param name="searchExpression">The beginning part of the title to be searched.</param>
        /// <returns>Search result.</returns>
        /// <remarks>This overload will allow up to 20 results to be returned, and will not resolve redirects.</remarks>
        public Task<IList<OpenSearchResultEntry>> OpenSearchAsync(string searchExpression)
        {
            return OpenSearchAsync(searchExpression, 20, 0, OpenSearchOptions.None, CancellationToken.None);
        }


        /// <summary>
        /// Performs an opensearch and get results, often used for search box suggestions.
        /// (MediaWiki 1.25 or OpenSearch extension)
        /// </summary>
        /// <param name="searchExpression">The beginning part of the title to be searched.</param>
        /// <param name="options">Other options.</param>
        /// <returns>Search result.</returns>
        /// <remarks>This overload will allow up to 20 results to be returned.</remarks>
        public Task<IList<OpenSearchResultEntry>> OpenSearchAsync(string searchExpression, OpenSearchOptions options)
        {
            return OpenSearchAsync(searchExpression, 20, 0, options, CancellationToken.None);
        }

        /// <summary>
        /// Performs an opensearch and get results, often used for search box suggestions.
        /// (MediaWiki 1.25 or OpenSearch extension)
        /// </summary>
        /// <param name="searchExpression">The beginning part of the title to be searched.</param>
        /// <param name="maxCount">Maximum number of results to return. No more than 500 (5000 for bots) allowed.
        /// prior to MW 1.28, this value was capped at 100, regardless of user permissions.</param>
        /// <returns>Search result.</returns>
        /// <remarks>This overload will not resolve redirects.</remarks>
        public Task<IList<OpenSearchResultEntry>> OpenSearchAsync(string searchExpression, int maxCount)
        {
            return OpenSearchAsync(searchExpression, maxCount, 0, OpenSearchOptions.None, CancellationToken.None);
        }

        /// <summary>
        /// Performs an opensearch and get results, often used for search box suggestions.
        /// (MediaWiki 1.25 or OpenSearch extension)
        /// </summary>
        /// <param name="searchExpression">The beginning part of the title to be searched.</param>
        /// <param name="maxCount">Maximum number of results to return. No more than 500 (5000 for bots) allowed.</param>
        /// <param name="options">Other options.</param>
        /// <returns>Search result.</returns>
        public Task<IList<OpenSearchResultEntry>> OpenSearchAsync(string searchExpression, int maxCount, OpenSearchOptions options)
        {
            return OpenSearchAsync(searchExpression, maxCount, 0, options, CancellationToken.None);
        }


        /// <summary>
        /// Performs an opensearch and get results, often used for search box suggestions.
        /// (MediaWiki 1.25 or OpenSearch extension)
        /// </summary>
        /// <param name="searchExpression">The beginning part of the title to be searched.</param>
        /// <param name="maxCount">Maximum number of results to return. No more than 500 (5000 for bots) allowed.</param>
        /// <param name="options">Other options.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <returns>Search result.</returns>
        public Task<IList<OpenSearchResultEntry>> OpenSearchAsync(string searchExpression, int maxCount, OpenSearchOptions options, CancellationToken cancellationToken)
        {
            return OpenSearchAsync(searchExpression, maxCount, 0, options, cancellationToken);
        }

        /// <summary>
        /// Performs an opensearch and get results, often used for search box suggestions.
        /// (MediaWiki 1.25 or OpenSearch extension)
        /// </summary>
        /// <param name="searchExpression">The beginning part of the title to be searched.</param>
        /// <param name="maxCount">Maximum number of results to return. No more than 500 (5000 for bots) allowed.</param>
        /// <param name="defaultNamespaceId">Default namespace id to search. See <see cref="BuiltInNamespaces"/> for a list of possible namespace ids.</param>
        /// <param name="options">Other options.</param>
        /// <returns>Search result.</returns>
        public Task<IList<OpenSearchResultEntry>> OpenSearchAsync(string searchExpression, int maxCount,
            int defaultNamespaceId, OpenSearchOptions options)
        {
            return OpenSearchAsync(searchExpression, maxCount, defaultNamespaceId, options, CancellationToken.None);
        }

        /// <summary>
        /// Performs an opensearch and get results, often used for search box suggestions.
        /// (MediaWiki 1.25 or OpenSearch extension)
        /// </summary>
        /// <param name="searchExpression">The beginning part of the title to be searched.</param>
        /// <param name="maxCount">Maximum number of results to return. No more than 500 (5000 for bots) allowed.</param>
        /// <param name="defaultNamespaceId">Default namespace id to search. See <see cref="BuiltInNamespaces"/> for a list of possible namespace ids.</param>
        /// <param name="options">Other options.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <returns>Search result.</returns>
        public async Task<IList<OpenSearchResultEntry>> OpenSearchAsync(string searchExpression, int maxCount,
            int defaultNamespaceId, OpenSearchOptions options, CancellationToken cancellationToken)
        {
            /*
[
    "Te",
    [
        "Te",
        "Television",
    ],
    [
        "From other capitalisation: ...",
        "Television or TV is ...",
    ],
    [
        "https://en.wikipedia.org/wiki/Te",
        "https://en.wikipedia.org/wiki/Television",
    ]
]
             */
            if (string.IsNullOrEmpty(searchExpression)) throw new ArgumentNullException(nameof(searchExpression));
            if (maxCount <= 0) throw new ArgumentOutOfRangeException(nameof(maxCount));
            var jresult = await PostValuesAsync(new
            {
                action = "opensearch",
                @namespace = defaultNamespaceId,
                search = searchExpression,
                limit = maxCount,
                redirects = (options & OpenSearchOptions.ResolveRedirects) == OpenSearchOptions.ResolveRedirects,
            }, cancellationToken);
            var result = new List<OpenSearchResultEntry>();
            var jarray = (JArray) jresult;
            var titles = jarray.Count > 1 ? (JArray) jarray[1] : null;
            var descs = jarray.Count > 2 ? (JArray) jarray[2] : null;
            var urls = jarray.Count > 3 ? (JArray)jarray[3] : null;
            if (titles != null)
            {
                for (int i = 0; i < titles.Count; i++)
                {
                    var entry = new OpenSearchResultEntry {Title = (string) titles[i]};
                    if (descs != null) entry.Description = (string) descs[i];
                    if (urls != null) entry.Url = (string) urls[i];
                    result.Add(entry);
                }
            }
            return result;
        }

        #endregion

        #region Parsing

        private IDictionary<string, object> BuildParsingParams(ParsingOptions options)
        {
            var p = new Dictionary<string, object>
            {
                {"action", "parse"},
                {"prop", "text|langlinks|categories|sections|revid|displaytitle|properties"},
                {"disabletoc", (options & ParsingOptions.DisableToc) == ParsingOptions.DisableToc},
                {"preview", (options & ParsingOptions.Preview) == ParsingOptions.Preview},
                {"sectionpreview", (options & ParsingOptions.SectionPreview) == ParsingOptions.SectionPreview},
                {"redirects", (options & ParsingOptions.ResolveRedirects) == ParsingOptions.ResolveRedirects},
                {"mobileformat", (options & ParsingOptions.MobileFormat) == ParsingOptions.MobileFormat},
                {"noimages", (options & ParsingOptions.NoImages) == ParsingOptions.NoImages},
                {"effectivelanglinks", (options & ParsingOptions.EffectiveLanguageLinks) == ParsingOptions.EffectiveLanguageLinks},
            };
            if ((options & ParsingOptions.TranscludedPages) == ParsingOptions.TranscludedPages)
                p["prop"] += "|templates";
            if ((options & ParsingOptions.LimitReport) == ParsingOptions.LimitReport)
                p["prop"] += "|limitreportdata";
            if ((options & ParsingOptions.DisableLimitReport) == ParsingOptions.DisableLimitReport)
            {
                if (SiteInfo.Version >= new Version("1.26"))
                    p["disablelimitreport"] = true;
                else
                    p["disablepp"] = true;
            }
            return p;
        }

        /// <summary>
        /// Parsing the specific page, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="title">Title of the page to be parsed.</param>
        /// <exception cref="ArgumentNullException"><paramref name="title"/> is <c>null</c>.</exception>
        /// <remarks>This overload will not follow the redirects.</remarks>
        public Task<ParsedContentInfo> ParsePageAsync(string title)
        {
            return ParsePageAsync(title, ParsingOptions.None, CancellationToken.None);
        }

        /// <summary>
        /// Parsing the specific page, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="title">Title of the page to be parsed.</param>
        /// <param name="options">Options for parsing.</param>
        /// <exception cref="ArgumentNullException"><paramref name="title"/> is <c>null</c>.</exception>
        public Task<ParsedContentInfo> ParsePageAsync(string title, ParsingOptions options)
        {
            return ParsePageAsync(title, options, new CancellationToken());
        }

        /// <summary>
        /// Parsing the specific page, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="title">Title of the page to be parsed.</param>
        /// <param name="options">Options for parsing.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <exception cref="ArgumentNullException"><paramref name="title"/> is <c>null</c>.</exception>
        public async Task<ParsedContentInfo> ParsePageAsync(string title, ParsingOptions options, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(title)) throw new ArgumentNullException(nameof(title));
            var p = BuildParsingParams(options);
            p["page"] = title;
            var jobj = await PostValuesAsync(p, cancellationToken);
            var parsed = ((JObject)jobj["parse"]).ToObject<ParsedContentInfo>(Utility.WikiJsonSerializer);
            return parsed;
        }

        /// <summary>
        /// Parsing the specific page, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="id">Id of the page to be parsed.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero or negative.</exception>
        /// <remarks>This overload will not follow the redirects.</remarks>
        public Task<ParsedContentInfo> ParsePageAsync(int id)
        {
            return ParsePageAsync(id, ParsingOptions.None, CancellationToken.None);
        }

        /// <summary>
        /// Parsing the specific page, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="id">Id of the page to be parsed.</param>
        /// <param name="options">Options for parsing.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero or negative.</exception>
        public Task<ParsedContentInfo> ParsePageAsync(int id, ParsingOptions options)
        {
            return ParsePageAsync(id, options, CancellationToken.None);
        }

        /// <summary>
        /// Parsing the specific page, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="id">Id of the page to be parsed.</param>
        /// <param name="options">Options for parsing.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero or negative.</exception>
        public async Task<ParsedContentInfo> ParsePageAsync(int id, ParsingOptions options, CancellationToken cancellationToken)
        {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            var p = BuildParsingParams(options);
            p["pageid"] = id;
            var jobj = await PostValuesAsync(p, cancellationToken);
            var parsed = ((JObject)jobj["parse"]).ToObject<ParsedContentInfo>(Utility.WikiJsonSerializer);
            return parsed;
        }

        /// <summary>
        /// Parsing the specific page revision, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="revId">Id of the revision to be parsed.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="revId"/> is zero or negative.</exception>
        public Task<ParsedContentInfo> ParseRevisionAsync(int revId)
        {
            return ParseRevisionAsync(revId, ParsingOptions.None, CancellationToken.None);
        }

        /// <summary>
        /// Parsing the specific page revision, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="revId">Id of the revision to be parsed.</param>
        /// <param name="options">Options for parsing.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="revId"/> is zero or negative.</exception>
        public Task<ParsedContentInfo> ParseRevisionAsync(int revId, ParsingOptions options)
        {
            return ParseRevisionAsync(revId, options, new CancellationToken());
        }

        /// <summary>
        /// Parsing the specific page revision, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="revId">Id of the revision to be parsed.</param>
        /// <param name="options">Options for parsing.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="revId"/> is zero or negative.</exception>
        public async Task<ParsedContentInfo> ParseRevisionAsync(int revId, ParsingOptions options, CancellationToken cancellationToken)
        {
            if (revId <= 0) throw new ArgumentOutOfRangeException(nameof(revId));
            var p = BuildParsingParams(options);
            p["oldid"] = revId;
            var jobj = await PostValuesAsync(p, cancellationToken);
            var parsed = ((JObject)jobj["parse"]).ToObject<ParsedContentInfo>(Utility.WikiJsonSerializer);
            return parsed;
        }

        /// <summary>
        /// Parsing the specific page content and/or summary, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="content">The content to parse.</param>
        /// <param name="summary">The summary to parse.</param>
        /// <param name="title">Act like the wikitext is on this page.
        /// This only really matters when parsing links to the page itself or subpages,
        /// or when using magic words like {{PAGENAME}}.
        /// If <c>null</c> is given, the default value "API" will be used.</param>
        /// <remarks>If both <paramref name="title"/> is <c>null</c>, the content model will be assumed as wikitext.</remarks>
        /// <param name="options">Options for parsing.</param>
        /// <remarks>The content model will be inferred from <paramref name="title"/>.</remarks>
        /// <exception cref="ArgumentException">Both <paramref name="content"/> and <paramref name="summary"/> is <c>null</c>.</exception>
        public Task<ParsedContentInfo> ParseContentAsync(string content, string summary, string title, ParsingOptions options)
        {
            return ParseContentAsync(content, summary, title, null, options, CancellationToken.None);
        }

        /// <summary>
        /// Parsing the specific page content and/or summary, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="content">The content to parse.</param>
        /// <param name="summary">The summary to parse. Can be <c>null</c>.</param>
        /// <param name="title">Act like the wikitext is on this page.
        ///     This only really matters when parsing links to the page itself or subpages,
        ///     or when using magic words like {{PAGENAME}}.
        ///     If <c>null</c> is given, the default value "API" will be used.</param>
        /// <param name="contentModel">The content model name of the text specified in <paramref name="content"/>. <c>null</c> makes the server to infer content model from <paramref name="title"/>.</param>
        /// <param name="options">Options for parsing.</param>
        /// <remarks>If both <paramref name="title"/> and <paramref name="contentModel"/> is <c>null</c>, the content model will be assumed as wikitext.</remarks>
        /// <exception cref="ArgumentException">Both <paramref name="content"/> and <paramref name="summary"/> is <c>null</c>.</exception>
        public Task<ParsedContentInfo> ParseContentAsync(string content, string summary, string title,
            string contentModel, ParsingOptions options)
        {
            return ParseContentAsync(content, summary, title, contentModel, options, new CancellationToken());
        }

        /// <summary>
        /// Parsing the specific page content and/or summary, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="content">The content to parse.</param>
        /// <param name="summary">The summary to parse. Can be <c>null</c>.</param>
        /// <param name="title">Act like the wikitext is on this page.
        ///     This only really matters when parsing links to the page itself or subpages,
        ///     or when using magic words like {{PAGENAME}}.
        ///     If <c>null</c> is given, the default value "API" will be used.</param>
        /// <param name="contentModel">The content model name of the text specified in <paramref name="content"/>. <c>null</c> makes the server to infer content model from <paramref name="title"/>.</param>
        /// <param name="options">Options for parsing.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <remarks>If both <paramref name="title"/> and <paramref name="contentModel"/> is <c>null</c>, the content model will be assumed as wikitext.</remarks>
        /// <exception cref="ArgumentException">Both <paramref name="content"/> and <paramref name="summary"/> is <c>null</c>.</exception>
        public async Task<ParsedContentInfo> ParseContentAsync(string content, string summary, string title,
            string contentModel, ParsingOptions options, CancellationToken cancellationToken)
        {
            if (content == null && summary == null) throw new ArgumentException(nameof(content));
            var p = BuildParsingParams(options);
            p["text"] = content;
            p["summary"] = summary;
            p["title"] = title;
            p["title"] = title;
            var jobj = await PostValuesAsync(p, cancellationToken);
            var parsed = ((JObject)jobj["parse"]).ToObject<ParsedContentInfo>(Utility.WikiJsonSerializer);
            return parsed;
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
    }

    /// <summary>
    /// Options for opensearch.
    /// </summary>
    [Flags]
    public enum OpenSearchOptions
    {
        /// <summary>No options.</summary>
        None = 0,

        /// <summary>
        /// Return the target page when meeting redirects.
        /// This may cause OpenSearch return fewer results than limitation.
        /// </summary>
        ResolveRedirects = 1,
    }

    /// <summary>
    /// Options for page or content parsing.
    /// </summary>
    [Flags]
    public enum ParsingOptions
    {
        None = 0,
        /// <summary>
        /// When parsing by page title or page id, returns the target page when meeting redirects.
        /// </summary>
        ResolveRedirects = 1,
        /// <summary>
        /// Disable table of contents in output. (1.23+)
        /// </summary>
        DisableToc = 2,
        /// <summary>
        /// Parse in preview mode. (1.22+)
        /// </summary>
        Preview = 4,
        /// <summary>
        /// Parse in section preview mode (enables preview mode too). (1.22+)
        /// </summary>
        SectionPreview = 8,
        /// <summary>
        /// Return parse output in a format suitable for mobile devices. (?)
        /// </summary>
        MobileFormat = 16,
        /// <summary>
        /// Disable images in mobile output. (?)
        /// </summary>
        NoImages = 0x20,
        /// <summary>
        /// Gives the structured limit report. (1.23+)
        /// This flag fills <see cref="ParsedContentInfo.ParserLimitReports"/>.
        /// </summary>
        LimitReport = 0x40,
        /// <summary>
        /// Omit the limit report ("NewPP limit report") from the parser output. (1.17+, disablepp; 1.23+, disablelimitreport)
        /// <see cref="ParsedContentInfo.ParserLimitReports"/> will be empty if both this flag and <see cref="LimitReport"/> is set.
        /// </summary>
        /// <remarks>By default, the limit report will be included as comment in the parsed HTML content.
        /// This flag can supress such output.</remarks>
        DisableLimitReport = 0x80,
        /// <summary>
        /// Includes language links supplied by extensions. (1.22+)
        /// </summary>
        EffectiveLanguageLinks = 0x100,
        /// <summary>
        /// Gives the templates and other transcluded pages/modules in the parsed wikitext.
        /// </summary>
        TranscludedPages = 0x200,
    }

    /// <summary>
    /// Represents an entry in opensearch result.
    /// </summary>
    public struct OpenSearchResultEntry
    {
        /// <summary>
        /// Title of the page.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Url of the page. May be null.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Description of the page. May be null.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 返回该实例的完全限定类型名。
        /// </summary>
        /// <returns>
        /// 包含完全限定类型名的 <see cref="T:System.String"/>。
        /// </returns>
        public override string ToString()
        {
            return Title + (Description != null ? ":" + Description : null);
        }
    }
}
