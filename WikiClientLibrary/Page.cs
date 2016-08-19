using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;

namespace WikiClientLibrary
{
    /// <summary>
    /// Represents a page on MediaWiki site.
    /// </summary>
    public class Page
    {
        public Page(Site site, string title)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (title == null) throw new ArgumentNullException(nameof(title));
            Site = site;
            WikiClient = Site.WikiClient;
            Debug.Assert(WikiClient != null);
            Title = title;
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (title == null) throw new ArgumentNullException(nameof(title));
        }

        public WikiClient WikiClient { get; }

        public Site Site { get; }

        public int Id { get; private set; }

        public int NamespaceId { get; private set; }

        public int LastRevisionId { get; private set; }

        public int ContentLength { get; private set; }

        /// <summary>
        /// Page touched timestamp.
        /// </summary>
        /// <remarks>See https://www.mediawiki.org/wiki/Manual:Page_table#page_touched .</remarks>
        public DateTime LastTouched { get; private set; }

        public IReadOnlyCollection<ProtectionInfo> Protections { get; private set; }

        public IReadOnlyCollection<string> RestrictionTypes { get; private set; }

        /// <summary>
        /// Gets whether the page exists.
        /// </summary>
        public bool Exists { get; private set; }

        /// <summary>
        /// Content model. (MediaWiki 1.22)
        /// </summary>
        public string ContentModel { get; private set; }

        /// <summary>
        /// Page language. (MediaWiki 1.24)
        /// </summary>
        /// <remarks>See https://www.mediawiki.org/wiki/API:PageLanguage .</remarks>
        public string PageLanguage { get; private set; }

        /// <summary>
        /// Gets the title of page. When more information is available,
        /// gets the normalized title of page.
        /// </summary>
        /// <remarks>
        /// Normalized title is a title with underscores(_) replaced by spaces,
        /// and the first letter is usually upper-case.
        /// </remarks>
        public string Title { get; private set; }

        public async Task RefreshInfoAsync()
        {
            var jobj = await WikiClient.GetJsonAsync(new
            {
                action = "query",
                prop = "info",
                inprop = "protection",
                titles = Title
            });
            var prop = ((JObject) jobj["query"]["pages"]).Properties().First();
            Id = Convert.ToInt32(prop.Name);
            var page = prop.Value;
            NamespaceId = (int) page["ns"];
            Title = (string) page["title"];
            Exists = page["missing"] == null;
            ContentModel = (string) page["contentmodel"];
            PageLanguage = (string) page["pagelanguage"];
            if (Exists)
            {
                ContentLength = (int) page["length"];
                LastRevisionId = (int) page["lastrevid"];
                LastTouched = (DateTime) page["touched"];
                Protections = ((JArray) page["protection"]).ToObject<IReadOnlyCollection<ProtectionInfo>>(
                    Utility.WikiJsonSerializer);
                RestrictionTypes = ((JArray) page["restrictiontypes"])?.ToObject<IReadOnlyCollection<string>>(
                    Utility.WikiJsonSerializer);
            }
            else
            {
                ContentLength = 0;
                LastRevisionId = 0;
                LastTouched = DateTime.MinValue;
                Protections = null;
            }
        }

        public async Task RefreshContentAsync()
        {
            
        }

        /// <summary>
        /// Gets / Sets the content of the page.
        /// </summary>
        /// <remarks>You should have invoked <see cref="RefreshContentAsync"/> before trying to read the content of the page.</remarks>
        public string Content { get; set; }
    }

    public struct ProtectionInfo
    {
        public string Type { get; set; }

        public string Level { get; set; }

        public DateTime Expiry { get; set; }

        public bool Cascade { get; set; }

        /// <summary>
        /// 返回该实例的完全限定类型名。
        /// </summary>
        /// <returns>
        /// 包含完全限定类型名的 <see cref="T:System.String"/>。
        /// </returns>
        public override string ToString()
        {
            return $"{Type}, {Level}, {Expiry}, {(Cascade ? "Cascade" : "")}";
        }
    }
}
