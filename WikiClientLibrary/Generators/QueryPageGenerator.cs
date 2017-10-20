using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Generators.Primitive;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// Get a list provided by a QueryPage-based special page. (MediaWiki 1.18)
    /// </summary>
    /// <remarks>See https://www.mediawiki.org/wiki/API:Querypage .</remarks>
    public class QueryPageGenerator : WikiPageGenerator<WikiPage>
    {
        public QueryPageGenerator(WikiSite site) : base(site)
        {
        }

        public QueryPageGenerator(WikiSite site, string queryPageName) : base(site)
        {
            QueryPageName = queryPageName;
        }

        /// <summary>
        /// Asynchronously get a list of available QueryPage-based special pages.
        /// The item in the list can later be used as a value of <see cref="QueryPageName"/>.
        /// </summary>
        /// <param name="site">MediaWiki site.</param>
        /// <returns>A list of titles of available QueryPage-based special pages.</returns>
        public static async Task<IList<string>> GetQueryPageNamesAsync(WikiSite site)
        {
            var module = await RequestHelper.QueryParameterInformationAsync(site, "query+querypage");
            var pa = module["parameters"].First(p => (string)p["name"] == "page");
            return ((JArray)pa["type"]).ToObject<IList<string>>();
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

        /// <inheritdoc />
        public override string ListName => "querypage";

        /// <inheritdoc/>
        public override IEnumerable<KeyValuePair<string, object>> EnumListParameters()
        {
            if (string.IsNullOrWhiteSpace(QueryPageName))
                throw new InvalidOperationException("Invalid QueryPageName.");
            return new Dictionary<string, object>
            {
                {"qppage", QueryPageName},
                {"qplimit", PaginationSize}
            };
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "QueryPage:" + QueryPageName;
        }
    }
}
