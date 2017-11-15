using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;
using AsyncEnumerableExtensions;
using WikiClientLibrary.Infrastructures.Logging;
using WikiClientLibrary.Pages.Queries;

namespace WikiClientLibrary
{
    /// <summary>
    /// Provides static methods for API queries.
    /// </summary>
    internal static class RequestHelper
    {
        #region Page/Revision query

        public static IAsyncEnumerable<JObject> QueryWithContinuation(WikiSite site,
            IEnumerable<KeyValuePair<string, object>> parameters,
            Func<IDisposable> beginActionScope,
            bool distinctPages = false)
        {
            return AsyncEnumerableFactory.FromAsyncGenerator<JObject>(async (sink, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                var retrivedPageIds = distinctPages ? new HashSet<int>() : null;
                using (beginActionScope?.Invoke())
                {
                    var queryParams = parameters.ToDictionary(p => p.Key, p => p.Value);
                    Debug.Assert("query".Equals(queryParams["action"]));
                    NEXT_PAGE:
                    var jresult = await site.GetJsonAsync(new MediaWikiFormRequestMessage(queryParams), ct);
                    // If there's no result, "query" node will not exist.
                    var queryNode = (JObject)jresult["query"];
                    if (queryNode != null)
                    {
                        var jpages = (JObject)queryNode["pages"];
                        if (retrivedPageIds != null && jpages != null)
                        {
                            // Remove duplicate results
                            var duplicateKeys = new List<string>(jpages.Count);
                            foreach (var jpage in jpages)
                            {
                                if (!retrivedPageIds.Add(Convert.ToInt32(jpage.Key)))
                                {
                                    // The page has been retrieved before.
                                    duplicateKeys.Add(jpage.Key);
                                }
                            }
                            var originalPageCount = jpages.Count;
                            foreach (var k in duplicateKeys) jpages.Remove(k);
                            if (originalPageCount != jpages.Count)
                            {
                                site.Logger.LogWarning(
                                    "Received {Count} results on {Site}, {DistinctCount} distinct results.",
                                    originalPageCount, site, jpages.Count);
                            }
                        }
                        await sink.YieldAndWait(queryNode);
                    }
                    var continuation = (JObject)(jresult["continue"]
                                                 ?? ((JProperty)jresult["query-continue"]?.First)?.Value);
                    // No more results.
                    if (continuation == null || continuation.Count == 0) return;
                    foreach (var p in continuation.Properties())
                    {
                        object parsed;
                        if (p.Value is JValue value) parsed = value.Value;
                        else parsed = p.Value.ToString(Formatting.None);
                        queryParams[p.Name] = parsed;
                    }
                    if (queryNode == null)
                        site.Logger.LogWarning("Empty query page with continuation received on {Site}.", site);
                    goto NEXT_PAGE;
                }
            });
        }

        /// <summary>
        /// Refresh a sequence of pages.
        /// </summary>
        public static async Task RefreshPagesAsync(IEnumerable<WikiPage> pages, IWikiPageQueryParameters options, CancellationToken cancellationToken)
        {
            if (pages == null) throw new ArgumentNullException(nameof(pages));
            // You can even fetch pages from different sites.
            foreach (var sitePages in pages.GroupBy(p => Tuple.Create(p.Site, p.GetType())))
            {
                var site = sitePages.Key.Item1;
                var queryParams = options.EnumParameters().ToDictionary();
                var titleLimit = options.GetMaxPaginationSize(site.AccountInfo.HasRight(UserRights.ApiHighLimits));
                using (site.BeginActionScope(sitePages, options))
                {
                    foreach (var partition in sitePages.Partition(titleLimit).Select(partition => partition.ToList()))
                    {
                        site.Logger.LogDebug("Fetching {Count} pages.", partition.Count);
                        // We use titles to query pages.
                        queryParams["titles"] = string.Join("|", partition.Select(p => p.Title));
                        // For single-page fetching, force fetching 1 revision only.
                        if (partition.Count == 1)
                            queryParams["rvlimit"] = 1;
                        else
                            queryParams.Remove("rvlimit");
                        var jobj = await site.GetJsonAsync(new MediaWikiFormRequestMessage(queryParams), cancellationToken);
                        // Process title normalization.
                        var normalized = jobj["query"]["normalized"]?.ToDictionary(n => (string)n["from"],
                            n => (string)n["to"]);
                        // Process redirects.
                        var redirects = jobj["query"]["redirects"]?.ToDictionary(n => (string)n["from"],
                            n => (string)n["to"]);
                        var pageInfoDict = ((JObject)jobj["query"]["pages"]).Properties()
                            .ToDictionary(p => (string)p.Value["title"]);
                        foreach (var page in partition)
                        {
                            var title = page.Title;
                            // Normalize the title first.
                            if (normalized?.ContainsKey(title) ?? false)
                                title = normalized[title];
                            // Then process the redirects.
                            var redirectTrace = new List<string>();
                            while (redirects?.ContainsKey(title) ?? false)
                            {
                                redirectTrace.Add(title); // Adds the last title
                                var next = redirects[title];
                                if (redirectTrace.Contains(next))
                                    throw new InvalidOperationException(
                                        $"Cannot resolve circular redirect: {string.Join("->", redirectTrace)}.");
                                title = next;
                            }
                            // Finally, get the page.
                            var pageInfo = pageInfoDict[title];
                            if (redirectTrace.Count > 0)
                                page.RedirectPath = redirectTrace;
                            page.LoadFromJson(pageInfo);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Refresh a sequence of revisions by revid, along with their owner pages.
        /// </summary>
        /// <remarks>
        /// <para>If there's invalid revision id in <paramref name="revIds"/>, an <see cref="ArgumentException"/> will be thrown while enumerating.</para>
        /// </remarks>
        public static IAsyncEnumerable<Revision> FetchRevisionsAsync(WikiSite site, IEnumerable<int> revIds, IWikiPageQueryParameters options,
            CancellationToken cancellationToken)
        {
            if (revIds == null) throw new ArgumentNullException(nameof(revIds));
            var queryParams = options.EnumParameters().ToDictionary();
            var titleLimit = options.GetMaxPaginationSize(site.AccountInfo.HasRight(UserRights.ApiHighLimits));
            return AsyncEnumerableFactory.FromAsyncGenerator<Revision>(async sink =>
            {
                // Page ID --> Page Stub
                var stubDict = new Dictionary<int, WikiPageStub>();
                var revDict = new Dictionary<int, Revision>();
                using (site.BeginActionScope(null, (object)revIds))
                {
                    foreach (var partition in revIds.Partition(titleLimit))
                    {
                        site.Logger.LogDebug("Fetching {Count} revisions from {Site}.", partition.Count, site);
                        queryParams["revids"] = string.Join("|", partition);
                        var jobj = await site.GetJsonAsync(new MediaWikiFormRequestMessage(queryParams), cancellationToken);
                        var jpages = (JObject)jobj["query"]["pages"];
                        // Generate stubs first
                        foreach (var p in jpages)
                        {
                            var jrevs = p.Value["revisions"];
                            if (jrevs == null || !jrevs.HasValues) continue;
                            var id = Convert.ToInt32(p.Key);
                            if (!stubDict.TryGetValue(id, out var stub))
                            {
                                stub = new WikiPageStub(id, (string)p.Value["title"], (int)p.Value["ns"]);
                                stubDict.Add(id, stub);
                            }
                            foreach (var jrev in jrevs)
                            {
                                var rev = jrev.ToObject<Revision>(Utility.WikiJsonSerializer);
                                rev.Page = stub;
                                revDict.Add(rev.Id, rev);
                            }
                        }
                        await sink.YieldAndWait(partition.Select(id => revDict.TryGetValue(id, out var rev) ? rev : null));
                    }
                }
            });
        }

        #endregion

        /// <summary>
        /// Asynchronously purges the pages.
        /// </summary>
        /// <returns>A collection of pages that haven't been successfully purged, because of either missing or invalid titles.</returns>
        public static async Task<IReadOnlyCollection<WikiPage>> PurgePagesAsync(IEnumerable<WikiPage> pages,
            PagePurgeOptions options, CancellationToken cancellationToken)
        {
            if (pages == null) throw new ArgumentNullException(nameof(pages));
            var failedPages = new List<WikiPage>();
            // You can even purge pages from different sites.
            foreach (var sitePages in pages.GroupBy(p => Tuple.Create(p.Site, p.GetType())))
            {
                var site = sitePages.Key.Item1;
                var titleLimit = site.AccountInfo.HasRight(UserRights.ApiHighLimits)
                    ? 500
                    : 50;
                using (site.BeginActionScope(sitePages, options))
                {
                    foreach (var partition in sitePages.Partition(titleLimit).Select(partition => partition.ToList()))
                    {
                        site.Logger.LogDebug("Purging {Count} pages on {Site}.", partition.Count, site);
                        // We purge pages by titles.
                        try
                        {
                            var jresult = await site.GetJsonAsync(new MediaWikiFormRequestMessage(new
                            {
                                action = "purge",
                                titles = string.Join("|", partition.Select(p => p.Title)),
                                forcelinkupdate =
                                (options & PagePurgeOptions.ForceLinkUpdate) == PagePurgeOptions.ForceLinkUpdate,
                                forcerecursivelinkupdate =
                                (options & PagePurgeOptions.ForceRecursiveLinkUpdate) ==
                                PagePurgeOptions.ForceRecursiveLinkUpdate,
                            }), cancellationToken);
                            // Now check whether the pages have been purged successfully.
                            // Process title normalization.
                            var normalized = jresult["normalized"]?.ToDictionary(n => (string)n["from"],
                                n => (string)n["to"]);
                            var purgeStatusDict = jresult["purge"].ToDictionary(o => o["title"]);
                            foreach (var page in partition)
                            {
                                var title = page.Title;
                                // Normalize the title.
                                if (normalized?.ContainsKey(title) ?? false)
                                    title = normalized[title];
                                // No redirects here ^_^
                                var jpage = purgeStatusDict[title];
                                if (jpage["invalid"] != null)
                                {
                                    site.Logger.LogWarning("Cannot purge the page: [[{Page}]]. {Reason}",
                                        page, jpage["invalidreason"]);
                                    failedPages.Add(page);
                                }
                                if (jpage["missing"] != null)
                                {
                                    site.Logger.LogWarning("Cannot purge the inexistent page: [[{Page}]].", page);
                                    failedPages.Add(page);
                                }
                            }
                        }
                        catch (OperationFailedException ex)
                        {
                            if (ex.ErrorCode == "cantpurge") throw new UnauthorizedOperationException(ex);
                            throw;
                        }
                    }
                }
            }
            return failedPages;
        }

        public static async Task PatrolAsync(WikiSite site, int? recentChangeId, int? revisionId, CancellationToken cancellationToken)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (recentChangeId == null && revisionId == null)
                throw new ArgumentNullException(nameof(recentChangeId),
                    "Either recentChangeId or revisionId should be set.");
            //if (recentChangeId != null && revisionId != null)
            //    throw new ArgumentException("Either recentChangeId or revisionId should be set, not both.");
            if (revisionId != null && site.SiteInfo.Version < new Version("1.22"))
                throw new InvalidOperationException("Current version of site does not support patrol by RevisionId.");
            var token = await site.GetTokenAsync("patrol");
            try
            {
                var jresult = await site.GetJsonAsync(new MediaWikiFormRequestMessage(new
                {
                    action = "patrol",
                    rcid = recentChangeId,
                    revid = revisionId,
                    token = token,
                }), cancellationToken);
                if (recentChangeId != null) Debug.Assert((int)jresult["patrol"]["rcid"] == recentChangeId.Value);
            }
            catch (OperationFailedException ex)
            {
                switch (ex.ErrorCode)
                {
                    case "nosuchrcid":
                        throw new ArgumentException($"There is no change with rcid {recentChangeId}.", ex);
                    case "patroldisabled":
                        throw new NotSupportedException("Patrolling is disabled on this wiki.", ex);
                    case "noautopatrol":
                        throw new UnauthorizedOperationException(
                            "You don't have permission to patrol your own changes. Only users with the autopatrol right can do this.",
                            ex);
                }
                throw;
            }
        }

        /// <summary>
        /// Queries parameter information for one module.
        /// </summary>
        /// <param name="site"></param>
        /// <param name="moduleName">Name of the module.</param>
        /// <returns>The paraminfo.modules[0] item.</returns>
        public static async Task<JObject> QueryParameterInformationAsync(WikiSite site, string moduleName)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            var pa = new Dictionary<string, object> { { "action", "paraminfo" } };
            if (site.SiteInfo.Version < new Version("1.25"))
            {
                var parts = moduleName.Split('+');
                switch (parts[0])
                {
                    case "main":
                        pa["mainmodule"] = true;
                        break;
                    case "query":
                        if (parts.Length == 1)
                            pa["pagesetmodule"] = true;
                        else
                            pa["querymodules"] = parts[1];
                        break;
                    case "format":
                        pa["formatmodules"] = true;
                        break;
                    default:
                        pa["modules"] = moduleName;
                        break;
                }
            }
            else
            {
                pa["modules"] = moduleName;
            }
            var jresult = await site.GetJsonAsync(new MediaWikiFormRequestMessage(pa), CancellationToken.None);
            var jmodules =
                ((JObject)jresult["paraminfo"]).Properties().FirstOrDefault(p => p.Name.EndsWith("modules"))?.Value;
            // For now we use the method internally.
            Debug.Assert(jmodules != null);
            return (JObject)jmodules.First;
        }

        /// <summary>
        /// Enumerate links from the page.
        /// </summary>
        public static IAsyncEnumerable<string> EnumLinksAsync(WikiSite site, string titlesExpr, /* optional */
            IEnumerable<int> namespaces)
        {
            var pa = new Dictionary<string, object>
            {
                {"action", "query"},
                {"prop", "links"},
                {"pllimit", site.ListingPagingSize},
                {"plnamespace", namespaces == null ? null : string.Join("|", namespaces)},
            };
            pa["titles"] = titlesExpr;
            var resultCounter = 0;
            return QueryWithContinuation(site, pa, null)
                .SelectMany(jquery =>
                {
                    var page = jquery["pages"].Values().First();
                    var links = (JArray)page?["links"];
                    if (links != null)
                    {
                        resultCounter += links.Count;
                        site.Logger.LogDebug("Loaded {Count} items linking to [[{Title}]] on {Site}.",
                            resultCounter, titlesExpr, site);
                        return links.Select(l => (string)l["title"]).ToAsyncEnumerable();
                    }
                    return AsyncEnumerable.Empty<string>();
                });
        }

        /// <summary>
        /// Enumerate transcluded pages trans from the page.
        /// </summary>
        public static IAsyncEnumerable<string> EnumTransclusionsAsync(WikiSite site, string titlesExpr,
            IEnumerable<int> namespaces = null, IEnumerable<string> transcludedTitlesExpr = null, int limit = -1)
        {
            // transcludedTitlesExpr should be full titles with ns prefix.
            var pa = new Dictionary<string, object>
            {
                {"action", "query"},
                {"prop", "templates"},
                {"tllimit", limit > 0 ? limit : site.ListingPagingSize},
                {"tlnamespace", namespaces == null ? null : string.Join("|", namespaces)},
                {"tltemplates", transcludedTitlesExpr == null ? null : string.Join("|", transcludedTitlesExpr)}
            };
            pa["titles"] = titlesExpr;
            var resultCounter = 0;
            return QueryWithContinuation(site, pa, null)
                .SelectMany(jquery =>
                {
                    var page = jquery["pages"].Values().First();
                    var links = (JArray)page?["templates"];
                    if (links != null)
                    {
                        resultCounter += links.Count;
                        site.Logger.LogDebug("Loaded {Count} items transcluded by [[{Title}]] on {Site}.",
                            resultCounter, titlesExpr, site);
                        return links.Select(l => (string)l["title"]).ToAsyncEnumerable();
                    }
                    return AsyncEnumerable.Empty<string>();
                });
        }
    }
}
