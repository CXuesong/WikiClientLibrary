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

        private Dictionary<int, NamespaceInfo> _Namespaces = new Dictionary<int, NamespaceInfo>();

        public ILogger Logger { get; set; }

        public static async Task<Site> GetAsync(WikiClient wikiClient)
        {
            var site = new Site(wikiClient);
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
                siprop = "general|namespaces|namespacealiases"
            });
            var qg = (JObject) jobj["query"]["general"];
            var ns = (JObject) jobj["query"]["namespaces"];
            //Name = (string) qg["sitename"];
            SiteInfo = qg.ToObject<SiteInfo>(Utility.WikiJsonSerializer);
            _Namespaces = ns.ToObject<Dictionary<int, NamespaceInfo>>(Utility.WikiJsonSerializer);
            Namespaces = new ReadOnlyDictionary<int, NamespaceInfo>(_Namespaces);
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
        }

        public SiteInfo SiteInfo { get; private set; }

        public UserInfo UserInfo { get; private set; }

        public IReadOnlyDictionary<int, NamespaceInfo> Namespaces { get; private set; }

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

        #region rendering

        /// <summary>
        /// Parsing the specific page, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        public async Task<ParsedContentInfo> ParsePage(string title, bool followRedirects)
        {
            if (title == null) throw new ArgumentNullException(nameof(title));
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
}
