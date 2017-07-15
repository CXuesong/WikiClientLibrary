using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary
{
    /// <summary>
    /// Provides static methods for API queries.
    /// </summary>
    internal static class RequestHelper
    {
        #region Page/Revision query

        /// <summary>
        /// Builds common parameters for fetching a page.
        /// </summary>
        private static IDictionary<string, object> GetPageFetchingParams(PageQueryOptions options)
        {
            var queryParams = new Dictionary<string, object>
            {
                {"action", "query"},
                // We also fetch category info, just in case.
                {"prop", "info|categoryinfo|imageinfo|revisions|pageprops"},
                {"inprop", "protection"},
                {"iiprop", "timestamp|user|comment|url|size|sha1" },
                {"rvprop", "ids|timestamp|flags|comment|user|contentmodel|sha1|tags|size"},
                {"redirects", (options & PageQueryOptions.ResolveRedirects) == PageQueryOptions.ResolveRedirects},
                {"maxlag", 5},
            };
            if ((options & PageQueryOptions.FetchContent) == PageQueryOptions.FetchContent)
            {
                queryParams["rvprop"] += "|content";
            }
            return queryParams;
        }

        /// <summary>
        /// Enumerate pages from the generator.
        /// </summary>
        public static IAsyncEnumerable<Page> EnumPagesAsync(PageGeneratorBase generator, PageQueryOptions options, int actualPagingSize)
        {
            if (generator == null) throw new ArgumentNullException(nameof(generator));
            if ((options & PageQueryOptions.ResolveRedirects) == PageQueryOptions.ResolveRedirects)
                throw new ArgumentException("Cannot resolve redirects when using generators.", nameof(options));
            var queryParams = GetPageFetchingParams(options);
            return generator.EnumJsonAsync(queryParams, actualPagingSize).SelectMany(jresult =>
            {
                var pages = Page.FromJsonQueryResult(generator.Site, jresult, options);
                generator.Site.Logger?.Trace(generator.Site, $"Loaded {pages.Count} pages from {generator}.");
                return pages.ToAsyncEnumerable();
            });
        }

        /// <summary>
        /// Refresh a sequence of pages.
        /// </summary>
        public static async Task RefreshPagesAsync(IEnumerable<Page> pages, PageQueryOptions options, CancellationToken cancellationToken)
        {
            if (pages == null) throw new ArgumentNullException(nameof(pages));
            // You can even fetch pages from different sites.
            foreach (var sitePages in pages.GroupBy(p => Tuple.Create(p.Site, p.GetType())))
            {
                var site = sitePages.Key.Item1;
                var queryParams = GetPageFetchingParams(options);
                var titleLimit = site.AccountInfo.HasRight(UserRights.ApiHighLimits)
                    ? 500
                    : 50;
                foreach (var partition in sitePages.Partition(titleLimit).Select(partition => partition.ToList()))
                {
                    site.Logger?.Trace(site, $"Fetching {partition.Count} pages.");
                    // We use titles to query pages.
                    queryParams["titles"] = string.Join("|", partition.Select(p => p.Title));
                    var jobj = await site.PostValuesAsync(queryParams, cancellationToken);
                    // Process title normalization.
                    var normalized = jobj["query"]["normalized"]?.ToDictionary(n => (string) n["from"],
                        n => (string) n["to"]);
                    // Process redirects.
                    var redirects = jobj["query"]["redirects"]?.ToDictionary(n => (string) n["from"],
                        n => (string) n["to"]);
                    var pageInfoDict = ((JObject) jobj["query"]["pages"]).Properties()
                        .ToDictionary(p => p.Value["title"]);
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
                        page.LoadFromJson(pageInfo, options);
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
        public static IAsyncEnumerable<Revision> FetchRevisionsAsync(WikiSite site, IEnumerable<int> revIds, PageQueryOptions options, CancellationToken cancellationToken)
        {
            if (revIds == null) throw new ArgumentNullException(nameof(revIds));
            var queryParams = GetPageFetchingParams(options);
            var titleLimit = site.AccountInfo.HasRight(UserRights.ApiHighLimits)
                ? 500
                : 50;
            // PageId --> JsonSerializer that can new Revision(Page)
            var pagePool = new Dictionary<int, JsonSerializer>();
            var revPartition = revIds.Partition(titleLimit).Select(partition => partition.ToList())
                .SelectAsync(async partition =>
                {
                    site.Logger?.Trace(site, $"Fetching {partition.Count} revisions.");
                    queryParams["revids"] = string.Join("|", partition);
                    var jobj = await site.PostValuesAsync(queryParams, cancellationToken);
                    var jpages = (JObject) jobj["query"]["pages"];
                    // Generate converters first
                    // Use DelegateCreationConverter to create Revision with constructor
                    var pages = Page.FromJsonQueryResult(site, (JObject) jobj["query"], options);
                    foreach (var p in pages)
                    {
                        if (!pagePool.ContainsKey(p.Id))
                        {
                            var p1 = p;
                            var serializer = Utility.CreateWikiJsonSerializer();
                            serializer.Converters.Add(new DelegateCreationConverter<Revision>(t => new Revision(p1)));
                            pagePool.Add(p.Id, serializer);
                        }
                    }
                    // Then convert revisions
                    var rawRev = jpages.Properties()
                        .SelectMany(p => p.Value["revisions"].Select(r => new
                        {
                            Serializer = pagePool[Convert.ToInt32(p.Name)],
                            RevisionId = (int) r["revid"],
                            Revision = r
                        })).ToDictionary(o => o.RevisionId);
                    return partition.Select(revId =>
                    {
                        try
                        {
                            var raw = rawRev[revId];
                            return raw.Revision.ToObject<Revision>(raw.Serializer);
                        }
                        catch (KeyNotFoundException)
                        {
                            throw new ArgumentException($"The revision id {revId} could not be found on the site.",
                                nameof(revIds));
                        }
                    }).ToAsyncEnumerable();
                });
            return revPartition.SelectMany(p => p);
        }

        /// <summary>
        /// Enumerate revisions from the page.
        /// </summary>
        /// <remarks>Redirect resolution is disabled in this operation.</remarks>
        public static IAsyncEnumerable<Revision> EnumRevisionsAsync(RevisionGenerator generator, PageQueryOptions options)
        {
            Debug.Assert(generator != null);
            Debug.Assert(generator.Page != null);
            Debug.Assert((options & PageQueryOptions.ResolveRedirects) != PageQueryOptions.ResolveRedirects);
            var site = generator.Site;
            var pa = GetPageFetchingParams(options);
            foreach (var p in generator.GetGeneratorParams()) pa[p.Key] = p.Value;
            var serializer = Utility.CreateWikiJsonSerializer();
            serializer.Converters.Add(new DelegateCreationConverter<Revision>(t => new Revision(generator.Page)));
            var resultCounter = 0;
            return new PagedQueryAsyncEnumerable(site, pa)
                .SelectMany(jresult =>
                {
                    var jpage = jresult["pages"].Values().First();
                    var revisions = (JArray)jpage?["revisions"];
                    if (revisions != null)
                    {
                        resultCounter += revisions.Count;
                        site.Logger?.Trace(site, $"Loaded {resultCounter} revisions of {generator.Page}.");
                        var result = revisions.ToObject<IList<Revision>>(serializer);
                        return result.ToAsyncEnumerable();
                    }
                    return AsyncEnumerable.Empty<Revision>();
                });
        }
        #endregion

        /// <summary>
        /// Asynchronously purges the pages.
        /// </summary>
        /// <returns>A collection of pages that haven't been successfully purged, because of either missing or invalid titles.</returns>
        public static async Task<IReadOnlyCollection<Page>> PurgePagesAsync(IEnumerable<Page> pages, 
            PagePurgeOptions options, CancellationToken cancellationToken)
        {
            if (pages == null) throw new ArgumentNullException(nameof(pages));
            var failedPages = new List<Page>();
            // You can even purge pages from different sites.
            foreach (var sitePages in pages.GroupBy(p => Tuple.Create(p.Site, p.GetType())))
            {
                var site = sitePages.Key.Item1;
                var titleLimit = site.AccountInfo.HasRight(UserRights.ApiHighLimits)
                    ? 500
                    : 50;
                foreach (var partition in sitePages.Partition(titleLimit).Select(partition => partition.ToList()))
                {
                    site.Logger?.Trace(site, $"Purging {partition.Count} pages.");
                    // We use titles to purge pages.
                    try
                    {
                        var jresult = await site.PostValuesAsync(new
                        {
                            action = "purge",
                            titles = string.Join("|", partition.Select(p => p.Title)),
                            forcelinkupdate =
                                (options & PagePurgeOptions.ForceLinkUpdate) == PagePurgeOptions.ForceLinkUpdate,
                            forcerecursivelinkupdate =
                                (options & PagePurgeOptions.ForceRecursiveLinkUpdate) ==
                                PagePurgeOptions.ForceRecursiveLinkUpdate,
                        }, cancellationToken);
                        // Now check whether the pages have been purged successfully.
                        // Process title normalization.
                        var normalized = jresult["normalized"]?.ToDictionary(n => (string) n["from"],
                            n => (string) n["to"]);
                        var purgeStatusDict = jresult["purge"].ToDictionary(o => o["title"]);
                        foreach (var page in partition)
                        {
                            var title = page.Title;
                            // Normalize the title.
                            if (normalized?.ContainsKey(title) ?? false)
                                title = normalized[title];
                            // No redirects here ^_^
                            var jpage = purgeStatusDict[title];
                            if (jpage["invalid"] != null || jpage["missing"] != null)
                            {
                                site.Logger?.Warn(site, $"Cannot purge the page: {page}. {jpage["invalidreason"]}");
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
                var jresult = await site.PostValuesAsync(new
                {
                    action = "patrol",
                    rcid = recentChangeId,
                    revid = revisionId,
                    token = token,
                }, cancellationToken);
                if (recentChangeId != null) Debug.Assert((int) jresult["patrol"]["rcid"] == recentChangeId.Value);
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
            var pa = new Dictionary<string, object> {{"action", "paraminfo"}};
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
            var jresult = await site.PostValuesAsync(pa, CancellationToken.None);
            var jmodules =
                ((JObject) jresult["paraminfo"]).Properties().FirstOrDefault(p => p.Name.EndsWith("modules"))?.Value;
            // For now we use the method internally.
            Debug.Assert(jmodules != null);
            return (JObject) jmodules.First;
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
            return new PagedQueryAsyncEnumerable(site, pa)
                .SelectMany(jquery =>
                {
                    var page = jquery["pages"].Values().First();
                    var links = (JArray) page?["links"];
                    if (links != null)
                    {
                        resultCounter += links.Count;
                        site.Logger?.Trace(site, $"Loaded {resultCounter} links out of {titlesExpr}.");
                        return links.Select(l => (string) l["title"]).ToAsyncEnumerable();
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
            return new PagedQueryAsyncEnumerable(site, pa)
                .SelectMany(jquery =>
                {
                    var page = jquery["pages"].Values().First();
                    var links = (JArray) page?["templates"];
                    if (links != null)
                    {
                        resultCounter += links.Count;
                        site.Logger?.Trace(site, $"Loaded {resultCounter} links out of {titlesExpr}.");
                        return links.Select(l => (string) l["title"]).ToAsyncEnumerable();
                    }
                    return AsyncEnumerable.Empty<string>();
                });
        }
    }
}
