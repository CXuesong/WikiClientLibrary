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

namespace WikiClientLibrary
{
    /// <summary>
    /// Represents a MediaWiki site.
    /// </summary>
    public class Site
    {
        public WikiClient WikiClient { get; }

        public ILogger Logger { get; set; }

        /// <summary>
        /// Initialize a <see cref="Site"/> instance with the given API Endpoint URL.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="wikiClient"/> or <paramref name="apiEndpoint"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="apiEndpoint"/> is invalid.</exception>
        /// <exception cref="UnauthorizedOperationException">Cannot access query API module due to target site permission settings. You may take a look at <see cref="SiteOptions.ExplicitInfoInitialization"/>.</exception>
        public static Task<Site> CreateAsync(WikiClient wikiClient, string apiEndpoint)
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
        /// <exception cref="UnauthorizedOperationException">Cannot access query API module due to target site permission settings. You may take a look at <see cref="SiteOptions.ExplicitInfoInitialization"/>.</exception>
        public static async Task<Site> CreateAsync(WikiClient wikiClient, SiteOptions options)
        {
            if (wikiClient == null) throw new ArgumentNullException(nameof(wikiClient));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrEmpty(options.ApiEndpoint))
                throw new ArgumentException("Invalid API endpoint url.", nameof(options));
            var site = new Site(wikiClient, options);
            if (options.DisambiguationTemplates != null)
            {
                site.disambiguationTemplates = options.DisambiguationTemplates
                    .Concat(new[] {SiteOptions.DefaultDisambiguationTemplate}).ToList();
            }
            if (!options.ExplicitInfoInitialization)
                await Task.WhenAll(site.RefreshSiteInfoAsync(), site.RefreshUserInfoAsync());
            return site;
        }

        /// <summary>
        /// Given a site or page URL of a MediaWiki site, try to look for the Api Endpoint URL of it.
        /// </summary>
        /// <param name="client">WikiClient instance.</param>
        /// <param name="urlExpression">The URL of MediaWiki site. It can be with or without protocol prefix.</param>
        /// <exception cref="ArgumentNullException"><paramref name="client"/> or <paramref name="urlExpression"/> is <c>null</c>.</exception>
        /// <returns>The URL of Api Endpoint. OR <c>null</c> if such search has failed.</returns>
        public static Task<string> SearchApiEndpointAsync(WikiClient client, string urlExpression)
        {
            return MediaWikiUtility.SearchApiEndpointAsync(client, urlExpression);
        }

        protected Site(WikiClient wikiClient, SiteOptions options)
        {
            // Perform basic checks.
            Debug.Assert(wikiClient != null);
            Debug.Assert(options != null);
            WikiClient = wikiClient;
            this.options = options;
            this.ApiEndpoint = options.ApiEndpoint;
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
            });
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
        /// Asserts that site info have been loaded.
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
        /// This method affects <see cref="UserInfo"/> property.
        /// </remarks>
        /// <exception cref="UnauthorizedOperationException">Cannot access query API module due to target site permission settings. You may need to login first.</exception>
        public async Task RefreshUserInfoAsync()
        {
            var jobj = await PostValuesAsync(new
            {
                action = "query",
                meta = "userinfo",
                uiprop = "blockinfo|groups|hasmsg|rights"
            });
            _UserInfo = ((JObject) jobj["query"]["userinfo"]).ToObject<UserInfo>(Utility.WikiJsonSerializer);
            ListingPagingSize = _UserInfo.HasRight(UserRights.ApiHighLimits) ? 5000 : 500;
        }

        private readonly SiteOptions options;
        private SiteInfo _SiteInfo;
        private UserInfo _UserInfo;
        private NamespaceCollection _Namespaces;
        private InterwikiMap _InterwikiMap;
        private ExtensionCollection _Extensions;

        public SiteInfo SiteInfo
        {
            get
            {
                AssertSiteInfoInitialized();
                return _SiteInfo;
            }
        }

        public UserInfo UserInfo
        {
            get
            {
                if (_UserInfo == null)
                    throw new InvalidOperationException(
                        "Site.RefreshUserInfoAsync should be successfully invoked before performing the operation.");
                return _UserInfo;
            }
        }

        public NamespaceCollection Namespaces
        {
            get
            {
                AssertSiteInfoInitialized();
                return _Namespaces;
            }
        }

        public InterwikiMap InterwikiMap
        {
            get
            {
                AssertSiteInfoInitialized();
                return _InterwikiMap;
            }
        }

        public ExtensionCollection Extensions
        {
            get
            {
                AssertSiteInfoInitialized();
                return _Extensions;
            }
        }

        public string ApiEndpoint { get; }

        /// <summary>
        /// Gets the default result limit per page for current user.
        /// </summary>
        /// <value>This value is 500 for user, and 5000 for bots.</value>
        // Use 500 for default. This value will be updated along with UserInfo.
        internal int ListingPagingSize { get; private set; } = 500;

        private List<string> disambiguationTemplates;

        /// <summary>
        /// Gets a list of titles of disambiguation templates. The default DAB template title
        /// will be included in the list.
        /// </summary>
        internal async Task<IEnumerable<string>> GetDisambiguationTemplatesAsync()
        {
            if (disambiguationTemplates == null)
            {
                var dabPages = await RequestManager
                    .EnumLinksAsync(this, "MediaWiki:Disambiguationspage", new[] {BuiltInNamespaces.Template})
                    .ToList();
                if (dabPages.Count == 0)
                {
                    // Try to fetch from mw messages
                    var msg = await GetMessageAsync("disambiguationspage");
                    if (msg != null) dabPages.Add(msg);
                }
                dabPages.Add(SiteOptions.DefaultDisambiguationTemplate);
                disambiguationTemplates = dabPages;
            }
            return disambiguationTemplates;
        }

        #region API

        /// <summary>
        /// Invokes API and get JSON result.
        /// </summary>
        /// <exception cref="InvalidActionException">Specified action is not supported.</exception>
        /// <exception cref="UnauthorizedOperationException">Permission denied.</exception>
        /// <exception cref="OperationFailedException">There's "error" node in returned JSON.</exception>
        /// <remarks>The request is sent via HTTP POST.</remarks>
        public Task<JToken> PostValuesAsync(IEnumerable<KeyValuePair<string, string>> queryParams)
        {
            return WikiClient.GetJsonAsync(options.ApiEndpoint, queryParams);
        }


        /// <summary>
        /// Invoke API and get JSON result.
        /// </summary>
        /// <param name="queryParams">An object whose proeprty-value pairs will be converted into key-value pairs and sent.</param>
        /// <exception cref="InvalidActionException">Specified action is not supported.</exception>
        /// <exception cref="OperationFailedException">There's "error" node in returned JSON.</exception>
        public Task<JToken> PostValuesAsync(object queryParams)
        {
            return WikiClient.GetJsonAsync(options.ApiEndpoint, queryParams);
        }

        // No, we cannot guarantee the returned value is JSON, so this function is internal.
        // It depends on caller's conscious.
        internal Task<JToken> PostContentAsync(HttpContent postContent)
        {
            return WikiClient.GetJsonAsync(options.ApiEndpoint, postContent);
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

        private Dictionary<string, string> _TokensCache = new Dictionary<string, string>();

        /// <summary>
        /// Fetch tokens. (MediaWiki 1.24)
        /// </summary>
        /// <param name="tokenTypeExpr">Token types, joined by | .</param>
        private async Task<JObject> FetchTokensAsync2(string tokenTypeExpr)
        {
            var jobj = await PostValuesAsync(new
            {
                action = "query",
                meta = "tokens",
                type = tokenTypeExpr,
            });
            var warnings = jobj["warnings"]?["tokens"];
            if (warnings != null)
            {
                // "*": "Unrecognized value for parameter 'type': xxxx"
                var warn = (string) warnings["*"];
                if (warn != null && warn.Contains("Unrecognized value") && warn.Contains("type"))
                    throw new ArgumentException(warn, nameof(tokenTypeExpr));
                throw new OperationFailedException(warnings.ToString());
            }
            return (JObject) jobj["query"]["tokens"];
        }

        /// <summary>
        /// Fetch tokens. (MediaWiki &lt; 1.24)
        /// </summary>
        /// <param name="tokenTypeExpr">Token types, joined by | .</param>
        private async Task<JObject> FetchTokensAsync(string tokenTypeExpr)
        {
            Debug.Assert(!tokenTypeExpr.Contains("patrol"));
            var jobj = await PostValuesAsync(new
            {
                action = "query",
                prop = "info",
                titles = "Dummy Title",
                intoken = tokenTypeExpr,
            });
            var page = (JObject) ((JProperty) jobj["query"]["pages"].First).Value;
            return new JObject(page.Properties().Where(p => p.Name.EndsWith("token")));
        }

        /// <summary>
        /// Request tokens for operations.
        /// </summary>
        /// <param name="tokenTypes">The names of token.</param>
        /// <remarks>See https://www.mediawiki.org/wiki/API:Tokens .</remarks>
        public Task<IDictionary<string, string>> GetTokensAsync(IEnumerable<string> tokenTypes)
        {
            return GetTokensAsync(tokenTypes, false);
        }

        /// <summary>
        /// Request tokens for operations.
        /// </summary>
        /// <param name="tokenTypes">The names of token. Names should be as accurate as possible (E.g. use "edit" instead of "csrf").</param>
        /// <param name="forceRefetch">Whether to fetch token from server, regardless of the cache.</param>
        /// <exception cref="InvalidOperationException">One or more specified token types cannot be recognized.</exception>
        /// <remarks>See https://www.mediawiki.org/wiki/API:Tokens .</remarks>
        public async Task<IDictionary<string, string>> GetTokensAsync(IEnumerable<string> tokenTypes, bool forceRefetch)
        {
            if (tokenTypes == null) throw new ArgumentNullException(nameof(tokenTypes));
            var tokenTypesList = tokenTypes as IReadOnlyList<string> ?? tokenTypes.ToList();
            var pendingtokens = tokenTypesList.Where(tt => forceRefetch || !_TokensCache.ContainsKey(tt)).ToList();
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
                if (SiteInfo.Version < new Version("1.20"))
                    needPatrolFromRC = pendingtokens.Remove("patrol");
                if (needPatrolFromRC)
                {
                    if (SiteInfo.Version < new Version("1.17"))
                    {
                        _TokensCache["patrol"] = await GetTokenAsync("edit");
                    }
                    else
                    {
                        var jobj = await PostValuesAsync(new
                        {
                            action = "query",
                            meta = "recentchanges",
                            rctoken = "patrol",
                            rclimit = 1
                        });
                        _TokensCache["patrol"] = (string) jobj["query"]["recentchanges"]["patroltoken"];
                    }
                }
                if (pendingtokens.Count > 0)
                    fetchedTokens = await FetchTokensAsync(string.Join("|", pendingtokens));
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
                    fetchedTokens = await FetchTokensAsync2(string.Join("|", pendingtokens));
                    var csrf = (string) fetchedTokens["csrftoken"];
                    if (csrf != null)
                    {
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
                    _TokensCache[tokenName] = (string) p.Value;
                    pendingtokens.Remove(tokenName);
                }
                if (pendingtokens.Count > 0)
                {
                    throw new InvalidOperationException("Unrecognized token(s): " + string.Join(", ", pendingtokens) +
                                                        ".");
                }
            }
            // Then return.
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
            return GetTokenAsync(tokenType, false);
        }

        /// <summary>
        /// Request a token for operation.
        /// </summary>
        /// <param name="tokenType">The name of token.</param>
        /// <param name="forceRefetch">Whether to fetch token from server, regardless of the cache.</param>
        /// <remarks>See https://www.mediawiki.org/wiki/API:Tokens .</remarks>
        public async Task<string> GetTokenAsync(string tokenType, bool forceRefetch)
        {
            if (tokenType == null) throw new ArgumentNullException(nameof(tokenType));
            if (tokenType.Contains("|"))
                throw new ArgumentException("Pipe character in token type name.", nameof(tokenType));
            var dict = await GetTokensAsync(new[] {tokenType}, forceRefetch);
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
        /// <remarks>This operation will refresh <see cref="UserInfo"/>.</remarks>
        public Task LoginAsync(string userName, string password)
        {
            return LoginAsync(userName, password, null);
        }

        /// <summary>
        /// Logins into the wiki site.
        /// </summary>
        /// <param name="userName">User name of the account.</param>
        /// <param name="password">Password of the account.</param>
        /// <param name="domain">Domain name. <c>null</c> is usually a good choice.</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="userName"/> or <paramref name="password"/> is <c>null</c> or empty.</exception>
        /// <remarks>This operation will refresh <see cref="UserInfo"/>.</remarks>
        public async Task LoginAsync(string userName, string password, string domain)
        {
            // Note: this method may be invoked BEFORE the initialization of _SiteInfo.
            if (string.IsNullOrEmpty(userName)) throw new ArgumentNullException(nameof(userName));
            if (string.IsNullOrEmpty(password)) throw new ArgumentNullException(nameof(password));
            string token = null;
            // For MedaiWiki 1.27+
            if (_SiteInfo?.Version >= new Version("1.27"))
                token = await GetTokenAsync("login", true);
            // For MedaiWiki < 1.27, We'll have to request twice.
            // If _SiteInfo hasn't been initialized, we just treat it as MedaiWiki < 1.27 .
            RETRY:
            var jobj = await PostValuesAsync(new
            {
                action = "login",
                lgname = userName,
                lgpassword = password,
                lgtoken = token,
                lgdomain = domain,
            });
            var result = (string) jobj["login"]["result"];
            string message = null;
            switch (result)
            {
                case "Success":
                    _TokensCache.Clear();
                    await RefreshUserInfoAsync();
                    Debug.Assert(UserInfo.IsUser);
                    return;
                case "Aborted":
                    message =
                        "The login using the main account password (rather than a bot password) cannot proceed because user interaction is required. The clientlogin action should be used instead.";
                    break;
                case "Throttled":
                    var time = (int) jobj["login"]["wait"];
                    Logger?.Warn($"Throttled: {time}sec.");
                    await Task.Delay(TimeSpan.FromSeconds(time));
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

        /// <summary>
        /// Logouts from the wiki site.
        /// </summary>
        /// <remarks>This operation will refresh <see cref="UserInfo"/>.</remarks>
        public async Task LogoutAsync()
        {
            var jobj = await PostValuesAsync(new
            {
                action = "logout",
            });
            _TokensCache.Clear();
            await RefreshUserInfoAsync();
        }

        #endregion

        #region Query

        private IDictionary<string, string> _CachedMessages = new Dictionary<string, string>();

        private async Task<JArray> FetchMessagesAsync(string messagesExpr)
        {
            var jresult = await PostValuesAsync(new
            {
                action = "query",
                meta = "allmessages",
                ammessages = messagesExpr,
            });
            return (JArray) jresult["query"]["allmessages"];
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
        public async Task<IDictionary<string, string>> GetMessagesAsync(IEnumerable<string> messages)
        {
            if (messages == null) throw new ArgumentNullException(nameof(messages));
            var impending = new List<string>();
            var result = new Dictionary<string, string>();
            foreach (var m in messages)
            {
                if (m == null) throw new ArgumentException("The sequence contains null item.", nameof(messages));
                if (m.Contains("|"))
                    throw new ArgumentException($"The message name \"{m}\" contains pipe character.", nameof(messages));
                if (m == "*") throw new InvalidOperationException("Getting all the messages is deprecated.");
                string content;
                if (_CachedMessages.TryGetValue(m.ToLowerInvariant(), out content))
                    result[m] = content;
                else
                    impending.Add(m);
            }
            if (impending.Count > 0)
            {
                var jr = await FetchMessagesAsync(string.Join("|", impending));
                foreach (var entry in jr)
                {
                    var name = (string) entry["name"];
                    //var nname = (string)entry["normalizedname"];
                    // for Wikia, there's no normalizedname
                    var message = (string) entry["*"];
                    //var missing = entry["missing"] != null;       message will be null
                    result[name] = message;
                    _CachedMessages[name] = message;
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
        public async Task<string> GetMessageAsync(string message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            var result = await GetMessagesAsync(new[] {message});
            return result.Values.FirstOrDefault();
        }

        /// <summary>
        /// Gets the statistical information of the MediaWiki site.
        /// </summary>
        public async Task<SiteStatistics> GetStatisticsAsync()
        {
            var jobj = await PostValuesAsync(new
            {
                action = "query",
                meta = "siteinfo",
                siprop = "statistics",
            });
            var jstat = (JObject) jobj["query"]?["statistics"];
            if (jstat == null) throw new UnexpectedDataException();
            var parsed = jstat.ToObject<SiteStatistics>();
            return parsed;
        }

        /// <summary>
        /// Parsing the specific page, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        public async Task<ParsedContentInfo> ParsePage(string title, bool followRedirects)
        {
            if (string.IsNullOrEmpty(title)) throw new ArgumentNullException(nameof(title));
            var jobj = await PostValuesAsync(new
            {
                action = "parse",
                page = title,
                redirects = followRedirects,
                prop = "text|langlinks|categories|sections|revid|displaytitle|properties|disabletoc"
            });
            var parsed = ((JObject) jobj["parse"]).ToObject<ParsedContentInfo>();
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
            return OpenSearchAsync(searchExpression, 20, OpenSearchOptions.None);
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
            return OpenSearchAsync(searchExpression, 20, options);
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
            return OpenSearchAsync(searchExpression, maxCount, OpenSearchOptions.None);
        }

        /// <summary>
        /// Performs an opensearch and get results, often used for search box suggestions.
        /// (MediaWiki 1.25 or OpenSearch extension)
        /// </summary>
        /// <param name="searchExpression">The beginning part of the title to be searched.</param>
        /// <param name="maxCount">Maximum number of results to return. No more than 500 (5000 for bots) allowed.</param>
        /// <param name="options">Other options.</param>
        /// <returns>Search result.</returns>
        public async Task<IList<OpenSearchResultEntry>> OpenSearchAsync(string searchExpression,
            int maxCount, OpenSearchOptions options)
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
                search = searchExpression,
                limit = maxCount,
                redirects = (options & OpenSearchOptions.ResolveRedirects) == OpenSearchOptions.ResolveRedirects,
            });
            var result = new List<OpenSearchResultEntry>();
            var titles = (JArray) jresult[1];
            var descs = jresult[2] as JArray;
            var urls = jresult[3] as JArray;
            for (int i = 0; i < titles.Count; i++)
            {
                var entry = new OpenSearchResultEntry {Title = (string) titles[i]};
                if (descs != null) entry.Description = (string) descs[i];
                if (urls != null) entry.Url = (string) urls[i];
                result.Add(entry);
            }
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

    /// <summary>
    /// Options for opensearch.
    /// </summary>
    [Flags]
    public enum OpenSearchOptions
    {
        None = 0,

        /// <summary>
        /// Return the target page when meeting redirects. May return fewer than limit results.
        /// </summary>
        ResolveRedirects = 1,
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

    /// <summary>
    /// Client options for creating a <see cref="Site"/> instance.
    /// </summary>
    public class SiteOptions
    {
        /// <summary>
        /// The name of default disambiguation template.
        /// </summary>
        /// <remarks>
        /// The default disambiguation template {{Disambig}} is always included in the
        /// list, implicitly.
        /// </remarks>
        public const string DefaultDisambiguationTemplate = "Template:Disambig";

        /// <summary>
        /// Specifies a list of disambiguation templates explicitly.
        /// </summary>
        /// <remarks>
        /// <para>This list is used when there's no Disambiguator on the MediaWiki site,
        /// and WikiClientLibrary is deciding wheter a page is a disambiguation page.
        /// The default disambiguation template {{Disambig}} is always included in the
        /// list, implicitly.</para>
        /// <para>If this value is <c>null</c>, WikiClientLibrary will try to
        /// infer the disambiguation template from [[MediaWiki:Disambiguationspage]].</para>
        /// </remarks>
        public IList<string> DisambiguationTemplates { get; set; }

        /// <summary>
        /// Sets the URL of MedaiWiki API endpoint.
        /// </summary>
        public string ApiEndpoint { get; set; }

        /// <summary>
        /// Whether to postpone the initialization of site info and user info
        /// until <see cref="Site.RefreshSiteInfoAsync"/> and <see cref="Site.RefreshUserInfoAsync"/>
        /// are called explicitly.
        /// </summary>
        /// <remarks>
        /// <para>This property affects the initialization of site info (<see cref="Site.SiteInfo"/>,
        /// <see cref="Site.Extensions"/>, <see cref="Site.InterwikiMap"/>,
        /// and <see cref="Site.Namespaces"/>), as well as <see cref="Site.UserInfo"/>. </para>
        /// <para>For the priviate wiki where anonymous users cannot access query API,
        /// it's recommended that this property be set to <c>true</c>.
        /// You can first check whether you have already logged in,
        /// and call <see cref="Site.LoginAsync(string,string)"/> If necessary.</para>
        /// <para>The site info and user info should always be initialized before most of the MediaWiki
        /// operations. Otherwise an <see cref="InvalidOperationException"/> will be thrown when
        /// attempting to perform those operations.</para>
        /// <para>In order to decide whether you have already logged in into a private wiki, you can
        /// <list type="number">
        /// <item><description>Call <see cref="Site.CreateAsync(WikiClient,SiteOptions)"/>, with <see cref="ExplicitInfoInitialization"/> set to <c>true</c>.</description></item>
        /// <item><description>Call and <c>await</c> for <see cref="Site.RefreshSiteInfoAsync"/> and/or <see cref="Site.RefreshUserInfoAsync"/>.</description></item>
        /// <item><description>If an <see cref="UnauthorizedOperationException"/> is raised, then you should call <see cref="Site.LoginAsync(string,string)"/> to login.</description></item>
        /// <item><description>Otherwise, check <see cref="UserInfo.IsAnnonymous"/>. Usually it would be <c>false</c>, since you've already logged in in a previous session.</description></item>
        /// </list>
        /// Note that <see cref="Site.RefreshUserInfoAsync"/> will be refreshed after a sucessful login operation,
        /// so you only have to call <see cref="Site.RefreshSiteInfoAsync"/> afterwards. Nonetheless, both the
        /// user info and the site info should be initially refreshed before you can perform other opertations.
        /// </para>
        /// </remarks>
        public bool ExplicitInfoInitialization { get; set; }

        /// <summary>
        /// Initializes with empty settings.
        /// </summary>
        public SiteOptions()
        {

        }

        /// <summary>
        /// Initializes with API endpoint URL.
        /// </summary>
        /// <param name="apiEndpoint">The URL of MedaiWiki API endpoint.</param>
        public SiteOptions(string apiEndpoint)
        {
            ApiEndpoint = apiEndpoint;
        }
    }
}
