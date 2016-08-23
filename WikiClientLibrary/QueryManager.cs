using System;
using System.Collections.Generic;
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
    internal static class QueryManager
    {
        private static IDictionary<string, string> GetPageFetchingParams(bool fetchContent)
        {
            var queryParams = new Dictionary<string, string>
            {
                {"action", "query"},
                {"prop", "info"},
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
        public static IAsyncEnumerable<Page> EnumPagesAsync(PageGenerator generator, bool fetchContent)
        {
            if (generator == null) throw new ArgumentNullException(nameof(generator));
            var queryParams = GetPageFetchingParams(fetchContent);
            return generator.EnumJsonAsync(queryParams).SelectMany(jresult =>
                Page.FromJsonQueryResult(generator.Site, (JObject) jresult["query"], fetchContent).ToAsyncEnumerable());
        }

        /// <summary>
        /// Refresh a sequence of pages.
        /// </summary>
        public static async Task RefreshPagesAsync(IEnumerable<Page> pages, bool fetchContent)
        {
            if (pages == null) throw new ArgumentNullException(nameof(pages));
            var queryParams = GetPageFetchingParams(fetchContent);
            foreach (var sitePages in pages.GroupBy(p => p.Site))
            {
                var titleLimit = sitePages.Key.UserInfo.HasRight(UserRights.ApiHighLimits)
                    ? 500
                    : 50;
                foreach (var partition in sitePages.Partition(titleLimit).Select(partition => partition.ToList()))
                {
                    sitePages.Key.Logger?.Trace($"Fetching {partition.Count} pages.");
                    queryParams["titles"] = string.Join("|", partition.Select(p => p.Title));
                    var jobj = await sitePages.Key.WikiClient.GetJsonAsync(queryParams);
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
                            page.LoadLastRevision(pageInfo.Value);
                        }
                    }
                }
            }
        }
    }
}
