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
        private static IDictionary<string, string> GetPageFetchingParams(bool fetchContent)
        {
            var queryParams = new Dictionary<string, string>
            {
                {"action", "query"},
                // We also fetch category info, just in case.
                {"prop", "info|categoryinfo"},
                {"inprop", "protection"},
                {"maxlag", "5"},
            };
            if (fetchContent)
            {
                queryParams["prop"] += "|revisions";
                queryParams["rvprop"] = "ids|timestamp|flags|comment|user|contentmodel|sha1|content";
            }
            return queryParams;
        }

        /// <summary>
        /// Enumerate pages from the generator.
        /// </summary>
        public static IAsyncEnumerable<T> EnumPagesAsync<T>(PageGeneratorBase generator, bool fetchContent)
            where T : Page
        {
            if (generator == null) throw new ArgumentNullException(nameof(generator));
            var queryParams = GetPageFetchingParams(fetchContent);
            return generator.EnumJsonAsync(queryParams).SelectMany(jresult =>
            {
                var jquery = (JObject) jresult["query"];
                return jquery == null
                    ? AsyncEnumerable.Empty<T>()
                    : Page.FromJsonQueryResult<T>(generator.Site, jquery, fetchContent)
                        .ToAsyncEnumerable();
            });
        }

        /// <summary>
        /// Refresh a sequence of pages.
        /// </summary>
        public static async Task RefreshPagesAsync(IEnumerable<Page> pages, bool fetchContent)
        {
            if (pages == null) throw new ArgumentNullException(nameof(pages));
            foreach (var sitePages in pages.GroupBy(p =>Tuple.Create(p.Site, p.GetType())))
            {
                var site = sitePages.Key.Item1;
                var queryParams = GetPageFetchingParams(fetchContent);
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
                    var pageInfoDict = ((JObject) jobj["query"]["pages"]).Properties()
                        .ToDictionary(p => p.Value["title"]);
                    foreach (var page in partition)
                    {
                        var title = page.Title;
                        if (normalized?.ContainsKey(page.Title) ?? false)
                            title = normalized[page.Title];
                        var pageInfo = pageInfoDict[title];
                        page.LoadPageInfo(pageInfo);
                        if (fetchContent)
                        {
                            // TODO Cache content
                            page.LoadLastRevision((JObject) pageInfo.Value);
                        }
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
