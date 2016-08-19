using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;

namespace WikiClientLibrary
{
    /// <summary>
    /// Represents a MediaWiki site.
    /// </summary>
    public class Site
    {
        public WikiClient WikiClient { get; }

        private Dictionary<int, NamespaceInfo> _Namespaces = new Dictionary<int, NamespaceInfo>();

        public static async Task<Site> GetAsync(WikiClient wikiClient)
        {
            var site = new Site(wikiClient);
            await site.RefreshAsync();
            return site;
        }

        protected Site(WikiClient wikiClient)
        {
            if (wikiClient == null) throw new ArgumentNullException(nameof(wikiClient));
            WikiClient = wikiClient;
            //Namespaces = new ReadOnlyDictionary<int, NamespaceInfo>(_Namespaces);
        }

        public async Task RefreshAsync()
        {
            var siteInfo = await WikiClient.GetJsonAsync(new
            {
                action = "query",
                meta = "siteinfo",
                siprop = "general|namespaces|namespacealiases"
            });
            var qg = (JObject) siteInfo["query"]["general"];
            var ns = (JObject) siteInfo["query"]["namespaces"];
            //Name = (string) qg["sitename"];
            Info = qg.ToObject<SiteInfo>(Utility.WikiJsonSerializer);
            _Namespaces = ns.ToObject<Dictionary<int, NamespaceInfo>>(Utility.WikiJsonSerializer);
            Namespaces = new ReadOnlyDictionary<int, NamespaceInfo>(_Namespaces);
        }

        public SiteInfo Info { get; private set; }

        public IReadOnlyDictionary<int, NamespaceInfo> Namespaces { get; private set; }

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        /// <returns>
        /// 表示当前对象的字符串。
        /// </returns>
        public override string ToString()
        {
            return string.IsNullOrEmpty(Info.SiteName) ? WikiClient.EndPointUrl : Info.SiteName;
        }
    }
}
