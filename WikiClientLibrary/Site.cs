using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
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

        public static Task<Site> GetAsync(WikiClient wikiClient)
        {
            return GetAsync(wikiClient, null);
        }

        public static async Task<Site> GetAsync(WikiClient wikiClient, SiteOptions options)
        {
            var site = new Site(wikiClient);
            if (options?.DisambiguationTemplates != null)
            {
                site.DisambiguationTemplates = options.DisambiguationTemplates
                    .Concat(new[] {SiteOptions.DefaultDisambiguationTemplate}).ToList();
            }
            await Task.WhenAll(site.RefreshSiteInfoAsync(), site.RefreshUserInfoAsync());
            return site;
        }

        protected Site(WikiClient wikiClient)
        {
            if (wikiClient == null) throw new ArgumentNullException(nameof(wikiClient));
            WikiClient = wikiClient;
            //Namespaces = new ReadOnlyDictionary<int, NamespaceInfo>(_Namespaces);
        }

        public async Task RefreshSiteInfoAsync()
        {
            var jobj = await WikiClient.GetJsonAsync(new
            {
                action = "query",
                meta = "siteinfo",
                siprop = "general|namespaces|namespacealiases|interwikimap|extensions"
            });
            var qg = (JObject) jobj["query"]["general"];
            var ns = (JObject) jobj["query"]["namespaces"];
            var aliases = (JArray) jobj["query"]["namespacealiases"];
            var interwiki = (JArray) jobj["query"]["interwikimap"];
            var extensions = (JArray)jobj["query"]["extensions"];
            SiteInfo = qg.ToObject<SiteInfo>(Utility.WikiJsonSerializer);
            Namespaces = new NamespaceCollection(this, ns, aliases);
            InterwikiMap = new InterwikiMap(this, interwiki);
            Extensions = new ExtensionCollection(this, extensions);
        }

        public async Task RefreshUserInfoAsync()
        {
            var jobj = await WikiClient.GetJsonAsync(new
            {
                action = "query",
                meta = "userinfo",
                uiprop = "blockinfo|groups|hasmsg|rights"
            });
            UserInfo = ((JObject) jobj["query"]["userinfo"]).ToObject<UserInfo>(Utility.WikiJsonSerializer);
            ListingPagingSize = UserInfo.HasRight(UserRights.ApiHighLimits) ? 5000 : 500;
        }

        public SiteOptions Options { get; set; }

        public SiteInfo SiteInfo { get; private set; }

        public UserInfo UserInfo { get; private set; }

        public NamespaceCollection Namespaces { get; private set; }

        public InterwikiMap InterwikiMap { get; private set; }

        public ExtensionCollection Extensions { get; private set; }

        /// <summary>
        /// Gets the default result limit per page for current user.
        /// </summary>
        /// <value>This value is 500 for user, and 5000 for bots.</value>
        // Use 500 for default. This value will be updated along with UserInfo.
        internal int ListingPagingSize { get; private set; } = 500;

        private List<string> DisambiguationTemplates;

        internal async Task<IEnumerable<string>> GetDisambiguationTemplatesAsync()
        {
            if (DisambiguationTemplates == null)
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
                DisambiguationTemplates = dabPages;
            }
            return DisambiguationTemplates;
        }

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
            var jobj = await WikiClient.GetJsonAsync(new
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
            var jobj = await WikiClient.GetJsonAsync(new
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
                        var jobj = await WikiClient.GetJsonAsync(new
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

        #region Authetication

        public Task LoginAsync(string userName, string password)
        {
            return LoginAsync(userName, password, null);
        }

        public async Task LoginAsync(string userName, string password, string domain)
        {
            if (string.IsNullOrEmpty(userName)) throw new ArgumentNullException(nameof(userName));
            if (string.IsNullOrEmpty(password)) throw new ArgumentNullException(nameof(password));
            string token = null;
            // For MedaiWiki 1.27+
            if (SiteInfo.Version >= new Version("1.27"))
                token = await GetTokenAsync("login", true);
            // For MedaiWiki < 1.27, We'll have to request twice.
            RETRY:
            var jobj = await WikiClient.GetJsonAsync(new
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
                case "WrongToken":   // We should have got correct token.
                    throw new UnexpectedDataException($"Unexpected login result: {result} .");
            }
            message = (string) jobj["login"]["reason"] ?? message;
            throw new OperationFailedException(result, message);
        }

        public async Task LogoutAsync()
        {
            var jobj = await WikiClient.GetJsonAsync(new
            {
                action = "logout",
            });
            await RefreshUserInfoAsync();
        }

        #endregion

        #region Query

        private IDictionary<string, string> _CachedMessages = new Dictionary<string, string>();

        private async Task<JArray> FetchMessagesAsync(string messagesExpr)
        {
            var jresult = await WikiClient.GetJsonAsync(new
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
                if (m.Contains("|")) throw new ArgumentException($"The message name \"{m}\" contains pipe character.", nameof(messages));
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
                    var name = (string)entry["name"];
                    //var nname = (string)entry["normalizedname"];
                    // for Wikia, there's no normalizedname
                    var message = (string)entry["*"];
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
            var jobj = await WikiClient.GetJsonAsync(new
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
            var jobj = await WikiClient.GetJsonAsync(new
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
            var jresult = await WikiClient.GetJsonAsync(new
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
            return string.IsNullOrEmpty(SiteInfo.SiteName) ? WikiClient.EndPointUrl : SiteInfo.SiteName;
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
    }
}
