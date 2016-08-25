using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// Get a list provided by a QueryPage-based special page. (MediaWiki 1.18)
    /// </summary>
    /// <remarks>See https://www.mediawiki.org/wiki/API:Querypage .</remarks>
    public class QueryPageGenerator : PageGenerator<Page>
    {
        public QueryPageGenerator(Site site) : base(site)
        {
        }

        public QueryPageGenerator(Site site, string queryPageName) : base(site)
        {
            QueryPageName = queryPageName;
        }

        /// <summary>
        /// Asynchronously get a list of available QueryPage-based special pages.
        /// The item in the list can later be used as a value of <see cref="QueryPageName"/>.
        /// </summary>
        /// <param name="site">MediaWiki site.</param>
        /// <returns>A list of titles of available QueryPage-based special pages.</returns>
        public static async Task<IList<string>> GetQueryPageNamesAsync(Site site)
        {
            var module = await RequestManager.QueryParameterInformationAsync(site, "query+querypage");
            var pa = module["parameters"].First(p => (string) p["name"] == "page");
            return ((JArray) pa["type"]).ToObject<IList<string>>();
        }

        /// <summary>
        /// Asynchronously get a list of available QueryPage-based special pages.
        /// The item in the list can later be used as a value of <see cref="QueryPageName"/>.
        /// </summary>
        /// <returns>A list of titles of available QueryPage-based special pages.</returns>
        public Task<IList<string>> GetQueryPageNamesAsync()
        {
            return GetQueryPageNamesAsync(Site);
        }

        /// <summary>
        /// The name of the special page. Note, this is case sensitive.
        /// </summary>
        public string QueryPageName { get; set; }

        /// <summary>
        /// When overridden, fills generator parameters for action=query request.
        /// </summary>
        /// <returns>The dictioanry containing request value pairs.</returns>
        protected override IEnumerable<KeyValuePair<string, object>> GetGeneratorParams()
        {
            if (string.IsNullOrWhiteSpace(QueryPageName))
                throw new InvalidOperationException("Invalid QueryPageName.");
            return new Dictionary<string, object>
            {
                {"generator", "querypage"},
                {"gqppage", QueryPageName},
                {"gqplimit", ActualPagingSize}
            };
        }

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        /// <returns>
        /// 表示当前对象的字符串。
        /// </returns>
        public override string ToString()
        {
            return "QueryPage:" + QueryPageName;
        }
    }
}
