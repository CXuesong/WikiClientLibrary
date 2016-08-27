using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Generators;

namespace WikiClientLibrary
{
    /// <summary>
    /// Provides static methods for API queries.
    /// </summary>
    internal static class RequestManager
    {
        private static IDictionary<string, object> GetPageFetchingParams(PageQueryOptions options)
        {
            var queryParams = new Dictionary<string, object>
            {
                {"action", "query"},
                // We also fetch category info, just in case.
                {"prop", "info|categoryinfo|revisions|pageprops"},
                {"inprop", "protection"},
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
        public static IAsyncEnumerable<Page> EnumPagesAsync(PageGeneratorBase generator, PageQueryOptions options)
        {
            if (generator == null) throw new ArgumentNullException(nameof(generator));
            if ((options & PageQueryOptions.ResolveRedirects) == PageQueryOptions.ResolveRedirects)
                throw new ArgumentException("Cannot resolve redirects when using generators.", nameof(options));
            var queryParams = GetPageFetchingParams(options);
            return generator.EnumJsonAsync(queryParams).SelectMany(jresult =>
            {
                var pages = Page.FromJsonQueryResult(generator.Site, jresult, options);
                generator.Site.Logger?.Trace($"Loaded {pages.Count} pages from {generator}.");
                return pages.ToAsyncEnumerable();
            });
        }

        /// <summary>
        /// Refresh a sequence of pages.
        /// </summary>
        public static async Task RefreshPagesAsync(IEnumerable<Page> pages, PageQueryOptions options)
        {
            if (pages == null) throw new ArgumentNullException(nameof(pages));
            foreach (var sitePages in pages.GroupBy(p => Tuple.Create(p.Site, p.GetType())))
            {
                var site = sitePages.Key.Item1;
                var queryParams = GetPageFetchingParams(options);
                var titleLimit = site.UserInfo.HasRight(UserRights.ApiHighLimits)
                    ? 500
                    : 50;
                foreach (var partition in sitePages.Partition(titleLimit).Select(partition => partition.ToList()))
                {
                    site.Logger?.Trace($"Fetching {partition.Count} pages.");
                    queryParams["titles"] = string.Join("|", partition.Select(p => p.Title));
                    var jobj = await site.WikiClient.GetJsonAsync(queryParams);
                    var normalized = jobj["query"]["normalized"]?.ToDictionary(n => (string) n["from"],
                        n => (string) n["to"]);
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
                            var next = redirects[title];
                            if (redirectTrace.Contains(next))
                            {
                                redirectTrace.Add(next);
                                throw new InvalidOperationException(
                                    $"Cannot resolve circular redirect: {string.Join("->", redirectTrace)}.");
                            }
                            redirectTrace.Add(next);
                            title = next;
                        }
                        // Finally, get the page.
                        var pageInfo = pageInfoDict[title];
                        page.LoadFromJson(pageInfo, options);
                    }
                }
            }
        }

        /// <summary>
        /// Enumerate revisions from the page.
        /// </summary>
        /// <remarks>Redirect resolution is disabled in this operation.</remarks>
        public static IAsyncEnumerable<Revision> EnumRevisionsAsync(Site site, string pageTitle,
            RevisionsQueryOptions options)
        {
            var pa = GetPageFetchingParams(
                (options & RevisionsQueryOptions.FetchContent) == RevisionsQueryOptions.FetchContent
                    ? PageQueryOptions.FetchContent
                    : PageQueryOptions.None);
            pa["rvlimit"] = site.ListingPagingSize;
            pa["rvdir"] = (options & RevisionsQueryOptions.TimeAscending) == RevisionsQueryOptions.TimeAscending
                ? "newer"
                : "older";
            pa["titles"] = pageTitle;
            var resultCounter = 0;
            return new PagedQueryAsyncEnumerable(site, pa)
                .SelectMany(jresult =>
                {
                    var page = jresult["query"]?["pages"].Values().First();
                    var revisions = (JArray) page?["revisions"];
                    if (revisions != null)
                    {
                        resultCounter += revisions.Count;
                        site.Logger?.Trace($"Loaded {resultCounter} revisions of {pageTitle}.");
                        return revisions.ToObject<IList<Revision>>(Utility.WikiJsonSerializer).ToAsyncEnumerable();
                    }
                    return AsyncEnumerable.Empty<Revision>();
                });
        }

        public static async Task PatrolAsync(Site site, int? recentChangeId, int? revisionId)
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
                var jresult = await site.WikiClient.GetJsonAsync(new
                {
                    action = "patrol",
                    rcid = recentChangeId,
                    revid = revisionId,
                    token = token,
                });
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
        public static async Task<JObject> QueryParameterInformationAsync(Site site, string moduleName)
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
            var jresult = await site.WikiClient.GetJsonAsync(pa);
            var jmodules =
                ((JObject) jresult["paraminfo"]).Properties().FirstOrDefault(p => p.Name.EndsWith("modules"))?.Value;
            // For now we use the method internally.
            Debug.Assert(jmodules != null);
            return (JObject) jmodules.First;
        }

        public static IAsyncEnumerable<string> EnumLinksAsync(Site site, string titlesExpr, /* optional */
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
                        site.Logger?.Trace($"Loaded {resultCounter} links out of {titlesExpr}.");
                        return links.Select(l => (string) l["title"]).ToAsyncEnumerable();
                    }
                    return AsyncEnumerable.Empty<string>();
                });
        }
    }
}
