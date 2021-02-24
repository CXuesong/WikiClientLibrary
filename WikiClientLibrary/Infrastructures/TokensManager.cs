using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Infrastructures.Logging;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Infrastructures
{
    /// <summary>
    /// Manages tokens for <see cref="WikiSite" />,
    /// </summary>
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

        private static readonly MediaWikiVersion v117 = new MediaWikiVersion(1, 17),
            v120 = new MediaWikiVersion(1, 20),
            v124 = new MediaWikiVersion(1, 24);

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
            var jobj = await site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new
            {
                action = "query",
                meta = "tokens",
                type = tokenTypeExpr,
            }), true, cancellationToken);
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
            var jobj = await site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new
            {
                action = "query",
                prop = "info",
                titles = "Dummy Title",
                intoken = tokenTypeExpr,
            }), true, cancellationToken);
            var page = (JObject)((JProperty)jobj["query"]["pages"].First).Value;
            return new JObject(page.Properties().Where(p => p.Name.EndsWith("token")));
        }

        /// <summary>
        /// Request a token for operation.
        /// </summary>
        /// <param name="tokenType">The name of token. Name should be as accurate as possible (E.g. use "edit" instead of "csrf").</param>
        /// <param name="forceRefetch">Whether to fetch token from server, regardless of the cache.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <exception cref="InvalidOperationException">One or more specified token types cannot be recognized.</exception>
        /// <remarks>
        /// <para>This method is thread-safe.</para>
        /// <para>See https://www.mediawiki.org/wiki/API:Tokens .</para>
        /// </remarks>
        public async Task<string> GetTokenAsync(string tokenType, bool forceRefetch, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(tokenType))
                throw new ArgumentException(Prompts.ExceptionArgumentNullOrEmpty, nameof(tokenType));
            if (tokenType.Contains("|"))
                throw new ArgumentException(Prompts.ExceptionArgumentContainsPipe, nameof(tokenType));
            cancellationToken.ThrowIfCancellationRequested();
            tokenType = tokenType.Trim();
            // Tokens that does not exist in local cache.
            // For Csrf tokens, only "csrf" will be included in the set.
            var realTokenType = tokenType;
            Task<string>? tokenTask = null;
            // Patrol was added in v1.14.
            // Until v1.16, the patrol token is same as the edit token.
            if (site.SiteInfo.Version < v117 && tokenType == "patrol")
                realTokenType = "edit";
            // Use csrf token if possible.
            if (site.SiteInfo.Version >= v124 && CsrfTokens.Contains(tokenType))
                realTokenType = "csrf";
            // Collect tokens from cache
            lock (tokensCache)
            {
                if (!forceRefetch && tokensCache.TryGetValue(realTokenType, out var value))
                {
                    if (value is string s) return s;
                    var task = (Task<string>)value;
                    if (task.Status == TaskStatus.RanToCompletion)
                    {
                        tokensCache[realTokenType] = task.Result;
                        return task.Result;
                    }
                    // If the task failed, we will retry here.
                    if (!task.IsCompleted)
                    {
                        tokenTask = task;
                    }
                }
                if (tokenTask == null)
                {
                    // Note that we won't actually cancel this request,
                    // we just give up waiting for it.
                    // This will prevent cancellation from one consumer from affecting other consumers.
                    tokenTask = FetchTokenAsyncCore(realTokenType);
                    tokensCache[realTokenType] = tokenTask;
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            // Wait for impending tokens.
            if (cancellationToken.CanBeCanceled)
            {
                var cancellationTcs = new TaskCompletionSource<string>();
                using (cancellationToken.Register(o => ((TaskCompletionSource<string>)o!).TrySetCanceled(),
                    cancellationTcs))
                {
                    // p.Value completes/failed, or cancellationTcs cancelled.
                    return await await Task.WhenAny(tokenTask, cancellationTcs.Task);
                }
            }
            else
            {
                return await tokenTask;
            }
        }

        private async Task<string> FetchTokenAsyncCore(string tokenType)
        {
            Debug.Assert(!string.IsNullOrEmpty(tokenType));

            string ExtractToken(JObject jTokens, string tokenType1)
            {
                if (jTokens == null) throw new ArgumentNullException(nameof(jTokens));
                var value = (string)(jTokens[tokenType1] ?? jTokens[tokenType1 + "token"]);
                if (value == null)
                    throw new ArgumentException($"Invalid token type: {tokenType1}.", nameof(tokenType));
                return value;
            }

            using (site.BeginActionScope(null, (object)tokenType))
            // We want to prevent the token fetching request get stuck. Anyway.
            using (var cts = new CancellationTokenSource(1000 * 180))
            {
                try
                {
                    if (site.SiteInfo.Version < v124)
                    {
                        /*
                         Patrol was added in v1.14.
                         Until v1.16, the patrol token is same as the edit token.
                         For v1.17-19, the patrol token must be obtained from the query
                         list recentchanges.
                         */
                        // Check whether we need a patrol token.
                        if (site.SiteInfo.Version < v120 && tokenType == "patrol")
                        {
                            // Until v1.16, the patrol token is same as the edit token.
                            Debug.Assert(site.SiteInfo.Version >= v117);
                            var jobj = await site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new
                            {
                                action = "query",
                                list = "recentchanges",
                                rctoken = "patrol",
                                rclimit = 1
                            }), cts.Token);
                            try
                            {
                                return ExtractToken((JObject)jobj["query"]["recentchanges"].First, "patroltoken");
                            }
                            catch (ArgumentException)
                            {
                                var warning = (string)jobj["warnings"]?["recentchanges"]?["*"];
                                // Action 'patrol' is not allowed for the current user
                                if (warning != null)
                                    throw new UnauthorizedOperationException(null, warning);
                                throw;
                            }
                        }
                        return ExtractToken(await FetchTokensAsync(tokenType, cts.Token), tokenType);
                    }
                    else
                    {
                        return ExtractToken(await FetchTokensAsync2(tokenType, cts.Token), tokenType);
                    }
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested)
                {
                    throw new TimeoutException();
                }
            }
        }

        public void ClearCache()
        {
            lock (tokensCache) tokensCache.Clear();
        }

        public void ClearCache(string tokenType)
        {
            lock (tokensCache)
            {
                if (tokenType == "patrol" && site.SiteInfo.Version < v117)
                    tokensCache.Remove("edit");
                else if (site.SiteInfo.Version >= v124 && CsrfTokens.Contains(tokenType))
                    tokensCache.Remove("csrf");
                tokensCache.Remove(tokenType);
            }
        }

    }
}
