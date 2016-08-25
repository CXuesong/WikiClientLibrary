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
                {"prop", "info|categoryinfo"},
                {"inprop", "protection"},
                {"redirects", (options & PageQueryOptions.ResolveRedirects) == PageQueryOptions.ResolveRedirects},
                {"maxlag", 5},
            };
            if ((options & PageQueryOptions.FetchLastRevision) == PageQueryOptions.FetchLastRevision)
            {
                queryParams["prop"] += "|revisions";
                queryParams["rvprop"] = "ids|timestamp|flags|comment|user|contentmodel|sha1|content";
            }
            return queryParams;
        }

        /// <summary>
        /// Enumerate pages from the generator.
        /// </summary>
        public static IAsyncEnumerable<T> EnumPagesAsync<T>(PageGeneratorBase generator, PageQueryOptions options)
            where T : Page
        {
            if (generator == null) throw new ArgumentNullException(nameof(generator));
            if ((options & PageQueryOptions.ResolveRedirects) == PageQueryOptions.ResolveRedirects)
                throw new ArgumentException("Cannot resolve redirects when using generators.", nameof(options));
            var queryParams = GetPageFetchingParams(options);
            return generator.EnumJsonAsync(queryParams).SelectMany(jresult =>
            {
                var jquery = (JObject) jresult["query"];
                return jquery == null
                    ? AsyncEnumerable.Empty<T>()
                    : Page.FromJsonQueryResult<T>(generator.Site, jquery, options)
                        .ToAsyncEnumerable();
            });
        }

        /// <summary>
        /// Refresh a sequence of pages.
        /// </summary>
        public static async Task RefreshPagesAsync(IEnumerable<Page> pages, PageQueryOptions options)
        {
            if (pages == null) throw new ArgumentNullException(nameof(pages));
            foreach (var sitePages in pages.GroupBy(p =>Tuple.Create(p.Site, p.GetType())))
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
                        // TODO Investigate how multi-redirects will be handled by API.
                        while (redirects?.ContainsKey(title) ?? false)
                            title = redirects[title];
                        // Finally, get the page.
                        var pageInfo = pageInfoDict[title];
                        page.LoadFromJson(pageInfo, options);
                    }
                }
            }
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
    }
}
