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
        private static IDictionary<string, string> GetPageFetchingParams(Type pageType, bool fetchContent)
        {
            var queryParams = new Dictionary<string, string>
            {
                {"action", "query"},
                {"prop", "info"},
                {"inprop", "protection"},
                {"maxlag", "5"},
            };
            if (pageType == typeof (Category)) queryParams["prop"] += "|categoryinfo";
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
            var queryParams = GetPageFetchingParams(typeof (T), fetchContent);
            return generator.EnumJsonAsync(queryParams).SelectMany(jresult =>
                Page.FromJsonQueryResult<T>(generator.Site, (JObject) jresult["query"], fetchContent)
                    .ToAsyncEnumerable());
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
                var queryParams = GetPageFetchingParams(sitePages.Key.Item2, fetchContent);
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
    }
}
