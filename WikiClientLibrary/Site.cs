using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WikiClientLibrary.Client;

namespace WikiClientLibrary
{
    /// <summary>
    /// Represents a MediaWiki site.
    /// </summary>
    public class Site
    {
        public WikiClient WikiClient { get; }

        public Site(WikiClient wikiClient)
        {
            if (wikiClient == null) throw new ArgumentNullException(nameof(wikiClient));
            WikiClient = wikiClient;
        }

        public async Task RefreshAsync()
        {
            var siteInfo = await WikiClient.GetJsonAsync(new {action = "query", meta = "siteinfo"});
            var qg = siteInfo["query"]["general"];
            Name = (string) qg["sitename"];
        }

        /// <summary>
        /// Site name. (query.general.sitename)
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        /// <returns>
        /// 表示当前对象的字符串。
        /// </returns>
        public override string ToString()
        {
            return string.IsNullOrEmpty(Name) ? WikiClient.EndPointUrl : Name;
        }
    }
}
