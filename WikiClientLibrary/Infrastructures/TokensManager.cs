using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Infrastructures
{
    internal sealed class TokensManager
    {

        // Value is string or Task<string>
        private readonly Dictionary<string, object> tokensCache = new Dictionary<string, object>();

        private readonly WikiSite site;

        // Tokens that have been merged into CSRF token since MediaWiki 1.24 .
        private static readonly string[] CsrfTokens =
        {
            "edit", "delete", "protect", "move", "block", "unblock", "email",
            "import"
        };

        private static readonly string[] CsrfTokensAndCsrf = CsrfTokens.Concat(new[] {"csrf"}).ToArray();

        public TokensManager(WikiSite site)
        {
            Debug.Assert(site != null);
            this.site = site;
        }

        /// <summary>
        /// Fetch tokens. (MediaWiki 1.24)
        /// </summary>
        /// <param name="tokenTypeExpr">Token types, joined by | .</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        private async Task<JObject> FetchTokensAsync2(string tokenTypeExpr, CancellationToken cancellationToken)
        {
            var jobj = await site.GetJsonAsync(new WikiFormRequestMessage(new
            {
                action = "query",
                meta = "tokens",
                type = tokenTypeExpr,
            }), true, cancellationToken);
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
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        private async Task<JObject> FetchTokensAsync(string tokenTypeExpr, CancellationToken cancellationToken)
        {
            Debug.Assert(!tokenTypeExpr.Contains("patrol"));
            var jobj = await site.GetJsonAsync(new WikiFormRequestMessage(new
            {
                action = "query",
                prop = "info",
                titles = "Dummy Title",
                intoken = tokenTypeExpr,
            }), true, cancellationToken);
            var page = (JObject) ((JProperty) jobj["query"]["pages"].First).Value;
            return new JObject(page.Properties().Where(p => p.Name.EndsWith("token")));
        }

        /// <summary>
        /// Request a token for operation.
        /// </summary>
        /// <param name="tokenType">The name of token.</param>
        /// <param name="forceRefetch">Whether to fetch token from server, regardless of the cache.</param>
        /// <remarks>See https://www.mediawiki.org/wiki/API:Tokens .</remarks>
        public async Task<string> GetTokenAsync(string tokenType, bool forceRefetch,
            CancellationToken cancellationToken)
        {
            var dict = await GetTokensAsync(new[] {tokenType}, forceRefetch, cancellationToken);
            return dict.Values.Single();
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
        public async Task<IDictionary<string, string>> GetTokensAsync(IEnumerable<string> tokenTypes, bool forceRefetch,
            CancellationToken cancellationToken)
        {
            if (tokenTypes == null) throw new ArgumentNullException(nameof(tokenTypes));
            cancellationToken.ThrowIfCancellationRequested();
            // Tokens that does not exist in local cache.
            // For Csrf tokens, only "csrf" will be included in the set.
            HashSet<string> missingTokens = null;
            HashSet<string> missingCsrfTokens = null;
            var result = new Dictionary<string, string>();
            Dictionary<string, Task<string>> impendingTokens = null;
            var csrfTokenAvailable = site.SiteInfo.Version >= new Version("1.24");

            async Task<string> SelectToken(Task<IList<string>> tokenListAsync, int index)
            {
                var token = (await tokenListAsync)[index];
                return token;
            }

            // Collect tokens from cache
            lock (tokensCache)
            {
                if (forceRefetch)
                {
                    missingTokens = new HashSet<string>(tokenTypes);
                    if (csrfTokenAvailable)
                    {
                        // Use csrf token if possible.
                        foreach (var tokenType in CsrfTokensAndCsrf)
                        {
                            if (missingTokens.Remove(tokenType))
                            {
                                missingTokens.Add("csrf");
                                if (missingCsrfTokens == null) missingCsrfTokens = new HashSet<string>();
                                missingCsrfTokens.Add(tokenType);
                            }
                        }
                    }
                }
                else
                {
                    foreach (var tokenType in tokenTypes)
                    {
                        if (string.IsNullOrEmpty(tokenType))
                            throw new ArgumentException("tokenTypes contains null or empty item.", nameof(tokenTypes));
                        if (tokenType.Contains("|"))
                            throw new ArgumentException("Pipe character detected in token type name.", nameof(tokenTypes));
                        // Use csrf token if possible.
                        var actualTokenKey = tokenType;
                        if (csrfTokenAvailable && CsrfTokens.Contains(tokenType))
                        {
                            actualTokenKey = "csrf";
                        }
                        if (tokensCache.TryGetValue(actualTokenKey, out var value))
                        {
                            if (value is string s)
                            {
                                result[tokenType] = s;
                            }
                            else
                            {
                                var task = (Task<string>) value;
                                if (task.Status == TaskStatus.RanToCompletion)
                                {
                                    tokensCache[actualTokenKey] = result[tokenType] = task.Result;
                                }
                                else if (task.IsCanceled || task.IsFaulted)
                                {
                                    // Retry failed attempts.
                                    goto SET_AS_MISSING;
                                }
                                else
                                {
                                    if (impendingTokens == null)
                                        impendingTokens = new Dictionary<string, Task<string>>();
                                    impendingTokens[actualTokenKey] = (Task<string>) value;
                                }
                            }
                            continue;
                        }
                        SET_AS_MISSING:
                        if (missingTokens == null) missingTokens = new HashSet<string>();
                        missingTokens.Add(actualTokenKey);
                        // Edge case: tokenTypes contains "csrf", then missingCsrfTokens will contain "csrf"
                        if (csrfTokenAvailable && actualTokenKey == "csrf")
                        {
                            if (missingCsrfTokens == null) missingCsrfTokens = new HashSet<string>();
                            missingCsrfTokens.Add(tokenType);
                        }
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (missingTokens != null)
                {
                    var requestTokens = missingTokens.ToList(); // Distinct list
                    // Note that we won't actually cancel this request,
                    // we just give up waiting for it.
                    // This will prevent cancellation from one consumer from affeting other consumers.
                    var fetchTokensTask = FetchTokensAsyncCore(requestTokens, CancellationToken.None);
                    for (var i = 0; i < requestTokens.Count; i++)
                    {
                        var task = SelectToken(fetchTokensTask, i);
                        tokensCache[requestTokens[i]] = task;
                        if (impendingTokens == null) impendingTokens = new Dictionary<string, Task<string>>();
                        impendingTokens.Add(requestTokens[i], task);
                    }
                }
            }
            if (impendingTokens != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Wait for impending tokens.
                if (cancellationToken.CanBeCanceled)
                {
                    var cancellationTcs = new TaskCompletionSource<string>();
                    using (cancellationToken.Register(o => ((TaskCompletionSource<string>) o).TrySetCanceled(),
                        cancellationTcs))
                    {
                        foreach (var p in impendingTokens)
                        {
                            // p.Value completes/failed, or cancellationTcs cancelled.
                            await await Task.WhenAny(p.Value, cancellationTcs.Task);
                        }
                    }
                }
                else
                {
                    await Task.WhenAll(impendingTokens.Values);
                }
                // Collect tokens
                foreach (var p in impendingTokens)
                {
                    var value = p.Value.Result;
                    if (csrfTokenAvailable && p.Key == "csrf")
                    {
                        Debug.Assert(missingCsrfTokens != null);
                        foreach (var csrfToken in missingCsrfTokens)
                        {
                            result.Add(csrfToken, value);
                        }
                    }
                    else
                    {
                        result.Add(p.Key, value);
                    }
                }
            }
            return result;
        }

        private async Task<IList<string>> FetchTokensAsyncCore(IList<string> tokenTypes,
            CancellationToken cancellationToken)
        {
            Debug.Assert(tokenTypes.Distinct().Count() == tokenTypes.Count);
            Debug.Assert(!cancellationToken.CanBeCanceled);
            JObject fetchedTokens = null;
            var localTokenTypes = tokenTypes;
            var tokens = new string[tokenTypes.Count];
            // We want to prevent the token fetching request get stuck. Anyway.
            using (var cts = new CancellationTokenSource(
                Math.Max(1000, (site.WikiClient.Timeout + site.WikiClient.RetryDelay).Milliseconds) *
                Math.Max(1, site.WikiClient.MaxRetries)))
                try
                {
                    cancellationToken = cts.Token;
                    if (site.SiteInfo.Version < new Version("1.24"))
                    {
                        /*
                         Patrol was added in v1.14.
                         Until v1.16, the patrol token is same as the edit token.
                         For v1.17-19, the patrol token must be obtained from the query
                         list recentchanges.
                         */
                        // Check whether we need a patrol token.
                        if (site.SiteInfo.Version < new Version("1.20"))
                        {
                            var patrolIndex = localTokenTypes.IndexOf("patrol");
                            if (patrolIndex >= 0)
                            {
                                string patrolToken;
                                if (site.SiteInfo.Version < new Version("1.17"))
                                {
                                    patrolToken = await GetTokenAsync("edit", false, cancellationToken);
                                }
                                else
                                {
                                    var jobj = await site.GetJsonAsync(new WikiFormRequestMessage(new
                                    {
                                        action = "query",
                                        list = "recentchanges",
                                        rctoken = "patrol",
                                        rclimit = 1
                                    }), cancellationToken);
                                    patrolToken = (string) jobj["query"]["recentchanges"].First?["patroltoken"];
                                    if (patrolToken == null)
                                    {
                                        var warning = (string) jobj["warnings"]?["recentchanges"]?["*"];
                                        // Action 'patrol' is not allowed for the current user
                                        if (warning != null)
                                            throw new UnauthorizedOperationException(null, warning);
                                    }
                                }
                                tokens[patrolIndex] = patrolToken; // <-- (A)
                                localTokenTypes = localTokenTypes.ToList();
                                localTokenTypes.Remove("patrol");
                            }
                        }
                        if (localTokenTypes.Count > 0)
                            fetchedTokens =
                                await FetchTokensAsync(string.Join("|", localTokenTypes), cancellationToken);
                    }
                    else
                    {
                        if (localTokenTypes.Count > 0)
                        {
                            fetchedTokens =
                                await FetchTokensAsync2(string.Join("|", localTokenTypes), cancellationToken);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    if (cts.IsCancellationRequested) throw new TimeoutException();
                }
            if (fetchedTokens == null) return tokens;
            for (var i = 0; i < tokenTypes.Count; i++)
            {
                var tt = tokenTypes[i];
                // For sake of (A)
                var value = (string) (fetchedTokens[tt + "token"] ?? fetchedTokens[tt]);
                if (value != null) tokens[i] = value;
            }
            return tokens;
        }

        public void ClearCache()
        {
            lock (tokensCache) tokensCache.Clear();
        }

        public void ClearCache(string tokenType)
        {
            lock (tokensCache)
            {
                if (tokenType == "patrol" && site.SiteInfo.Version < new Version("1.17"))
                    tokensCache.Remove("edit");
                tokensCache.Remove(tokenType);
            }
        }

    }
}
