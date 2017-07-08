using System;
using System.Collections.Concurrent;
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
    public partial class Site
    {

        private readonly SiteOptions options;

        #region Services

        /// <summary>
        /// Gets the <see cref="WikiClientBase" /> used to perform the requests.
        /// </summary>
        public WikiClientBase WikiClient { get; }

        /// <summary>
        /// Gets or sets the <see cref="ILogger"/> used to log the requests.
        /// </summary>
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
            if (queryParams == null) throw new ArgumentNullException(nameof(queryParams));
            return PostValuesAsync(Utility.ToWikiStringValuePairs(queryParams), supressAccountAssertion, cancellationToken);
        }

        /// <summary>
        /// Invokes API and gets JSON result.
        /// </summary>
        /// <param name="postContentFactory">The factory function that returns a new <see cref="HttpContent"/> per invocation.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <exception cref="ArgumentException"><paramref name="postContentFactory" /> returns <c>null</c> for the first invocation.</exception>
        /// <exception cref="InvalidActionException">Specified action is not supported.</exception>
        /// <exception cref="UnauthorizedOperationException">Permission denied.</exception>
        /// <exception cref="OperationFailedException">There's "error" node in returned JSON.</exception>
        /// <remarks>
        /// <para>If <paramref name="postContentFactory" /> returns <c>null</c> for the first invocation, an
        /// <see cref="ArgumentException"/> will be thrown. If it returns <c>null</c> for subsequent invocations
        /// (often when retrying the request), no further retry will be performed.</para>
        /// <para>You need to specify format=json manually in the request content.</para>
        /// </remarks>
        internal Task<JToken> PostContentAsync(Func<HttpContent> postContentFactory, CancellationToken cancellationToken)
        {
            if (postContentFactory == null) throw new ArgumentNullException(nameof(postContentFactory));
            return WikiClient.GetJsonAsync(options.ApiEndpoint, postContentFactory, cancellationToken);
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

        private readonly SemaphoreSlim fetchTokensAsyncCoreSemaphore = new SemaphoreSlim(1, 1);

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
        public async Task<IDictionary<string, string>> GetTokensAsync(IEnumerable<string> tokenTypes, bool forceRefetch,
            CancellationToken cancellationToken)
        {
            if (tokenTypes == null) throw new ArgumentNullException(nameof(tokenTypes));
            List<string> pendingtokens = null;
            var result = new Dictionary<string, string>();
            lock (_TokensCache)
            {
                foreach (var tt in tokenTypes)
                {
                    if (string.IsNullOrEmpty(tt))
                        throw new ArgumentException("tokenTypes contains null or empty item.", nameof(tokenTypes));
                    if (forceRefetch || !_TokensCache.TryGetValue(tt, out var value))
                    {
                        if (pendingtokens == null) pendingtokens = new List<string>();
                        pendingtokens.Add(tt);
                    }
                    else
                    {
                        result[tt] = value;
                    }
                }
            }
            if (pendingtokens != null)
            {
                await fetchTokensAsyncCoreSemaphore.WaitAsync(cancellationToken);
                try
                {
                    // In case some tokens have just been fetched…
                    if (!forceRefetch)
                    {
                        lock (_TokensCache)
                        {
                            for (int i = 0; i < pendingtokens.Count; i++)
                            {
                                if (_TokensCache.TryGetValue(pendingtokens[i], out var value))
                                {
                                    result[pendingtokens[i]] = value;
                                    pendingtokens.RemoveAt(i);
                                    i--;
                                }
                            }
                        }
                    }
                    await FetchTokensAsyncCore(pendingtokens, cancellationToken);
                }
                finally
                {
                    fetchTokensAsyncCoreSemaphore.Release();
                }
                lock (_TokensCache)
                {
                    foreach (var key in pendingtokens)
                    {
                        if (_TokensCache.TryGetValue(key, out var value))
                        {
                            result[key] = value;
                        }
                        else
                        {
                            throw new InvalidOperationException("Unrecognized token: " + key + ".");
                        }
                    }
                }
            }
            return result;
        }

        public async Task FetchTokensAsyncCore(IList<string> tokenTypes, CancellationToken cancellationToken)
        {
            JObject fetchedTokens = null;
            var localTokenTypes = tokenTypes.ToList();
            if (SiteInfo.Version < new Version("1.24"))
            {
                /*
                 Patrol was added in v1.14.
                 Until v1.16, the patrol token is same as the edit token.
                 For v1.17-19, the patrol token must be obtained from the query
                 list recentchanges.
                 */
                // Check whether we need a patrol token.
                if (SiteInfo.Version < new Version("1.20") && localTokenTypes.Remove("patrol"))
                {
                    if (!_TokensCache.ContainsKey("patrol"))
                    {
                        string patrolToken;
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
                            patrolToken = (string) jobj["query"]["recentchanges"]["patroltoken"];
                        }
                        _TokensCache["patrol"] = patrolToken;
                    }
                }
                if (localTokenTypes.Count > 0)
                    fetchedTokens = await FetchTokensAsync(string.Join("|", localTokenTypes), cancellationToken);
            }
            else
            {
                // Use csrf token if possible.
                if (!localTokenTypes.Contains("csrf"))
                {
                    var needCsrf = false;
                    foreach (var t in CsrfTokens)
                    {
                        if (localTokenTypes.Remove(t)) needCsrf = true;
                    }
                    if (needCsrf) localTokenTypes.Add("csrf");
                }
                if (localTokenTypes.Count > 0)
                {
                    fetchedTokens = await FetchTokensAsync2(string.Join("|", localTokenTypes), cancellationToken);
                    var csrf = (string) fetchedTokens["csrftoken"];
                    if (csrf != null)
                    {
                        foreach (var t in CsrfTokens) _TokensCache[t] = csrf;
                    }
                }
            }
            // Put tokens into cache first.
            if (fetchedTokens == null) return;
            foreach (var p in fetchedTokens.Properties())
            {
                // Remove "token" in the result
                var tokenName = p.Name.EndsWith("token")
                    ? p.Name.Substring(0, p.Name.Length - 5)
                    : p.Name;
                    _TokensCache[tokenName] = (string) p.Value;
            }
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
            return LoginAsync(userName, password, domain, CancellationToken.None);
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
                    lock (_TokensCache) _TokensCache.Clear();
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
            lock (_TokensCache) _TokensCache.Clear();
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

}
