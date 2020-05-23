using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
using WikiClientLibrary.Pages.Queries.Properties;

namespace WikiClientLibrary
{
    /// <summary>
    /// Provides static methods for API queries.
    /// </summary>
    internal static class RequestHelper
    {

        public const int CONTINUATION_DONE = 0;
        public const int CONTINUATION_AVAILABLE = 1;
        public const int CONTINUATION_LOOP = 2;

        public static JObject FindQueryContinuationParameterRoot(JToken jresult)
        {
            return (JObject)(jresult["continue"] ?? ((JProperty)jresult["query-continue"]?.First)?.Value);
        }

        public static int ParseContinuationParameters(JToken jresult, IDictionary<string, object> queryParams, IDictionary<string, object> continuationParams)
        {
            var continuation = FindQueryContinuationParameterRoot(jresult);
            // No more results.
            if (continuation == null || continuation.Count == 0)
                return CONTINUATION_DONE;
            var anyNewValue = false;
            continuationParams?.Clear();
            foreach (var p in continuation.Properties())
            {
                object parsed;
                if (p.Value is JValue value) parsed = value.Value;
                else parsed = p.Value.ToString(Formatting.None);
                if (!queryParams.TryGetValue(p.Name, out var existingValue) || !ValueEquals(existingValue, parsed))
                    anyNewValue = true;
                continuationParams?.Add(new KeyValuePair<string, object>(p.Name, parsed));
            }
            return anyNewValue ? CONTINUATION_AVAILABLE : CONTINUATION_LOOP;

            bool ValueEquals(object existing, object incoming)
            {
                if (Equals(existing, incoming)) return true;
                if (existing is DateTime dt && incoming is string s)
                {
                    if (MediaWikiHelper.TryParseDateTime(s, out var dt2))
                    {
                        // We have called ToUniversalTime() in ToWikiStringValuePairs.
                        return dt.ToUniversalTime() == dt2.ToUniversalTime();
                    }
                }
                return false;
            }
        }

        public static JToken FindQueryResponseItemsRoot(JToken jresult, string actionName)
        {
            // If there's no result, "query" node will not exist.
            var queryNode = (JObject)jresult["query"];
            if (queryNode != null && queryNode.HasValues)
            {
                var listNode = queryNode[actionName];
                if (listNode == null)
                {
                    if (queryNode.Count > 1)
                        throw new UnexpectedDataException(Prompts.ExceptionWikiListCannotFindResultRoot);
                    listNode = ((JProperty)queryNode.First).Value;
                }
                return listNode;
            }
            return null;
        }

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
                    var baseQueryParams = parameters.ToDictionary(p => p.Key, p => p.Value);
                    Debug.Assert("query".Equals(baseQueryParams["action"]));
                    var continuationParams = new Dictionary<string, object>();
                    while (true)
                    {
                        var queryParams = new Dictionary<string, object>(baseQueryParams);
                        queryParams.MergeFrom(continuationParams);
                        var jresult = await site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(queryParams), ct);
                        var jpages = (JObject)FindQueryResponseItemsRoot(jresult, "pages");
                        if (jpages != null)
                        {
                            if (retrivedPageIds != null)
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
                            await sink.YieldAndWait(jpages);
                        }
                        switch (ParseContinuationParameters(jresult, queryParams, continuationParams))
                        {
                            case CONTINUATION_DONE:
                                return;
                            case CONTINUATION_AVAILABLE:
                                if (jpages == null)
                                    site.Logger.LogWarning("Empty query page with continuation received on {Site}.", site);
                                // Continue the loop and fetch for the next page of query.
                                break;
                            case CONTINUATION_LOOP:
                                throw new UnexpectedDataException();
                        }
                    }
                }
            });
        }

        private struct WikiPageGroupKey : IEquatable<WikiPageGroupKey>
        {

            public readonly WikiSite Site;

            public readonly bool HasTitle;

            public WikiPageGroupKey(WikiPage page)
            {
                if (page == null) throw new ArgumentNullException(nameof(page));
                Site = page.Site;
                HasTitle = page.PageStub.HasTitle;
            }

            /// <inheritdoc />
            public bool Equals(WikiPageGroupKey other)
            {
                return Site.Equals(other.Site) && HasTitle == other.HasTitle;
            }

            /// <inheritdoc />
            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                return obj is WikiPageGroupKey && Equals((WikiPageGroupKey)obj);
            }

            /// <inheritdoc />
            public override int GetHashCode()
            {
                unchecked
                {
                    return (Site == null ? 0 : Site.GetHashCode() * 397) ^ HasTitle.GetHashCode();
                }
            }

            public static bool operator ==(WikiPageGroupKey left, WikiPageGroupKey right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(WikiPageGroupKey left, WikiPageGroupKey right)
            {
                return !left.Equals(right);
            }
        }

        /// <summary>
        /// Refresh a sequence of pages.
        /// </summary>
        public static async Task RefreshPagesAsync(IEnumerable<WikiPage> pages, IWikiPageQueryProvider options, CancellationToken cancellationToken)
        {
            if (pages == null) throw new ArgumentNullException(nameof(pages));
            // You can even fetch pages from different sites.
            foreach (var sitePages in pages.GroupBy(p => new WikiPageGroupKey(p)))
            {
                var site = sitePages.Key.Site;
                var queryParams = options.EnumParameters(site.SiteInfo.Version).ToDictionary();
                var titleLimit = options.GetMaxPaginationSize(site.SiteInfo.Version, site.AccountInfo.HasRight(UserRights.ApiHighLimits));
                using (site.BeginActionScope(sitePages, options))
                {
                    foreach (var partition in sitePages.Partition(titleLimit))
                    {
                        if (sitePages.Key.HasTitle)
                        {
                            // If a page has both title and ID information,
                            // we will use title anyway.
                            site.Logger.LogDebug("Fetching {Count} pages by title.", partition.Count);
                            queryParams["titles"] = MediaWikiHelper.JoinValues(partition.Select(p => p.Title));
                        }
                        else
                        {
                            site.Logger.LogDebug("Fetching {Count} pages by ID.", partition.Count);
                            Debug.Assert(sitePages.All(p => p.PageStub.HasId));
                            queryParams["pageids"] = MediaWikiHelper.JoinValues(partition.Select(p => p.Id));
                        }
                        var jobj = await site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(queryParams), cancellationToken);
                        var jquery = (JObject)jobj["query"];
                        var continuationStatus = ParseContinuationParameters(jobj, queryParams, null);
                        // Process continuation caused by props (e.g. langlinks) that contain a list that is too long.
                        if (continuationStatus != CONTINUATION_DONE)
                        {
                            var queryParams1 = new Dictionary<string, object>();
                            var continuationParams = new Dictionary<string, object>();
                            var jobj1 = jobj;
                            ParseContinuationParameters(jobj1, queryParams1, continuationParams);
                            while (continuationStatus != CONTINUATION_DONE)
                            {
                                if (continuationStatus == CONTINUATION_LOOP)
                                    throw new UnexpectedDataException(Prompts.ExceptionUnexpectedContinuationLoop);
                                Debug.Assert(continuationStatus == CONTINUATION_AVAILABLE);
                                site.Logger.LogDebug("Detected query continuation. PartitionCount={PartitionCount}.", partition.Count);
                                queryParams1.Clear();
                                queryParams1.MergeFrom(queryParams);
                                queryParams1.MergeFrom(continuationParams);
                                jobj1 = await site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(queryParams1), cancellationToken);
                                var jquery1 = jobj1["query"];
                                if (jquery1.HasValues)
                                {
                                    // Merge JSON response
                                    jquery.Merge(jquery1);
                                }
                                continuationStatus = ParseContinuationParameters(jobj1, queryParams1, continuationParams);
                            }
                        }
                        if (sitePages.Key.HasTitle)
                        {
                            // Process title normalization.
                            var normalized = jquery["normalized"]?.ToDictionary(n => (string)n["from"], n => (string)n["to"]);
                            // Process redirects.
                            var redirects = jquery["redirects"]?.ToDictionary(n => (string)n["from"], n => (string)n["to"]);
                            var pageInfoDict = ((JObject)jquery["pages"]).Properties()
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
                                        throw new InvalidOperationException(string.Format(Prompts.ExceptionWikiPageResolveCircularRedirect1, string.Join("->", redirectTrace)));
                                    title = next;
                                }
                                // Finally, get the page.
                                var pageInfo = pageInfoDict[title];
                                if (redirectTrace.Count > 0)
                                    page.RedirectPath = redirectTrace;
                                MediaWikiHelper.PopulatePageFromJson(page, (JObject)pageInfo.Value, options);
                            }
                        }
                        else
                        {
                            foreach (var page in partition)
                            {
                                var jPage = (JObject)jquery["pages"][page.Id.ToString(CultureInfo.InvariantCulture)];
                                MediaWikiHelper.PopulatePageFromJson(page, jPage, options);
                            }
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
        public static IAsyncEnumerable<Revision> FetchRevisionsAsync(WikiSite site, IEnumerable<int> revIds, IWikiPageQueryProvider options, CancellationToken cancellationToken)
        {
            if (revIds == null) throw new ArgumentNullException(nameof(revIds));
            var queryParams = options.EnumParameters(site.SiteInfo.Version).ToDictionary();
            // Remove any rvlimit magic word generated by RevisionsPropertyProvider.
            // We are only fetching by revisions.
            queryParams.Remove("rvlimit");
            var titleLimit = options.GetMaxPaginationSize(site.SiteInfo.Version, site.AccountInfo.HasRight(UserRights.ApiHighLimits));
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
                        queryParams["revids"] = MediaWikiHelper.JoinValues(partition);
                        var jobj = await site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(queryParams), cancellationToken);
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

        private static readonly IReadOnlyCollection<PurgeFailureInfo> emptyPurgeFailures = new PurgeFailureInfo[0];

        /// <summary>
        /// Asynchronously purges the pages.
        /// </summary>
        /// <returns>A collection of pages that haven't been successfully purged, because of either missing or invalid titles.</returns>
        public static async Task<IReadOnlyCollection<PurgeFailureInfo>> PurgePagesAsync(IEnumerable<WikiPage> pages, PagePurgeOptions options, CancellationToken cancellationToken)
        {
            if (pages == null) throw new ArgumentNullException(nameof(pages));
            List<PurgeFailureInfo> failedPages = null;
            // You can even purge pages from different sites.
            foreach (var sitePages in pages.GroupBy(p => new WikiPageGroupKey(p)))
            {
                var site = sitePages.Key.Site;
                var titleLimit = site.AccountInfo.HasRight(UserRights.ApiHighLimits)
                    ? 500
                    : 50;
                using (site.BeginActionScope(sitePages, options))
                {
                    foreach (var partition in sitePages.Partition(titleLimit).Select(partition => partition.ToList()))
                    {
                        string titles;
                        string ids;
                        if (sitePages.Key.HasTitle)
                        {
                            // If a page has both title and ID information,
                            // we will use title anyway.
                            site.Logger.LogDebug("Purging {Count} pages by title.", partition.Count);
                            titles = MediaWikiHelper.JoinValues(partition.Select(p => p.Title));
                            ids = null;
                        }
                        else
                        {
                            site.Logger.LogDebug("Purging {Count} pages by ID.", partition.Count);
                            Debug.Assert(sitePages.All(p => p.PageStub.HasId));
                            titles = null;
                            ids = MediaWikiHelper.JoinValues(partition.Select(p => p.Id));
                        }
                        try
                        {
                            var jresult = await site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new
                            {
                                action = "purge",
                                titles = titles,
                                pageids = ids,
                                forcelinkupdate = (options & PagePurgeOptions.ForceLinkUpdate) == PagePurgeOptions.ForceLinkUpdate,
                                forcerecursivelinkupdate = (options & PagePurgeOptions.ForceRecursiveLinkUpdate) == PagePurgeOptions.ForceRecursiveLinkUpdate,
                            }), cancellationToken);
                            // Now check whether the pages have been purged successfully.
                            foreach (var jitem in jresult["purge"])
                            {
                                if (jitem["missing"] != null || jitem["invalid"] != null)
                                {
                                    if (failedPages == null) failedPages = new List<PurgeFailureInfo>();
                                    failedPages.Add(new PurgeFailureInfo(MediaWikiHelper.PageStubFromJson((JObject)jitem), (string)jitem["invalidreason"]));
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
            return failedPages ?? emptyPurgeFailures;
        }

        public static async Task PatrolAsync(WikiSite site, int? recentChangeId, int? revisionId, CancellationToken cancellationToken)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (recentChangeId == null && revisionId == null)
                throw new ArgumentNullException(nameof(recentChangeId), "Either recentChangeId or revisionId should be set.");
            //if (recentChangeId != null && revisionId != null)
            //    throw new ArgumentException("Either recentChangeId or revisionId should be set, not both.");
            if (revisionId != null && site.SiteInfo.Version < new MediaWikiVersion(1, 22))
                throw new InvalidOperationException(Prompts.ExceptionPatrolledByRevisionNotSupported);
            var token = await site.GetTokenAsync("patrol");
            try
            {
                var jresult = await site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new
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
                        throw new ArgumentException(string.Format(Prompts.ExceptionPatrolNoSuchRcid1, recentChangeId), ex);
                    case "patroldisabled":
                        throw new NotSupportedException(Prompts.ExceptionPatrolDisabled, ex);
                    case "noautopatrol":
                        throw new UnauthorizedOperationException(Prompts.ExceptionPatrolNoAutoPatrol, ex);
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
            if (site.SiteInfo.Version < new MediaWikiVersion(1, 25))
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
            var jresult = await site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(pa), CancellationToken.None);
            var jmodules = ((JObject)jresult["paraminfo"]).Properties().FirstOrDefault(p => p.Name.EndsWith("modules"))?.Value;
            // For now we use the method internally.
            Debug.Assert(jmodules != null);
            return (JObject)jmodules.First;
        }

        /// <summary>
        /// Enumerate links from the page.
        /// </summary>
        public static IAsyncEnumerable<string> EnumLinksAsync(WikiSite site, string titlesExpr, /* optional */ IEnumerable<int> namespaces)
        {
            var pa = new Dictionary<string, object>
            {
                {"action", "query"}, {"prop", "links"}, {"pllimit", site.ListingPagingSize}, {"plnamespace", namespaces == null ? null : MediaWikiHelper.JoinValues(namespaces)},
            };
            pa["titles"] = titlesExpr;
            var resultCounter = 0;
            return QueryWithContinuation(site, pa, null)
                .SelectMany(jpages =>
                {
                    var page = jpages.Values().First();
                    var links = (JArray)page?["links"];
                    if (links != null)
                    {
                        resultCounter += links.Count;
                        site.Logger.LogDebug("Loaded {Count} items linking to [[{Title}]] on {Site}.", resultCounter, titlesExpr, site);
                        return links.Select(l => (string)l["title"]).ToAsyncEnumerable();
                    }
                    return AsyncEnumerable.Empty<string>();
                });
        }

        /// <summary>
        /// Enumerate transcluded pages trans from the page.
        /// </summary>
        public static IAsyncEnumerable<string> EnumTransclusionsAsync(WikiSite site, string titlesExpr, IEnumerable<int> namespaces = null, IEnumerable<string> transcludedTitlesExpr = null, int limit = -1)
        {
            // transcludedTitlesExpr should be full titles with ns prefix.
            var pa = new Dictionary<string, object>
            {
                {"action", "query"}, {"prop", "templates"}, {"tllimit", limit > 0 ? limit : site.ListingPagingSize}, {"tlnamespace", namespaces == null ? null : MediaWikiHelper.JoinValues(namespaces)}, {"tltemplates", transcludedTitlesExpr == null ? null : MediaWikiHelper.JoinValues(transcludedTitlesExpr)}
            };
            pa["titles"] = titlesExpr;
            var resultCounter = 0;
            return QueryWithContinuation(site, pa, null)
                .SelectMany(jpages =>
                {
                    var page = jpages.Values().First();
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
