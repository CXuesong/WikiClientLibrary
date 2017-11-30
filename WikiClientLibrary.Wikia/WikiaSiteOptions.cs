using System;
using System.Collections.Generic;
using System.Text;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Sites;
using WikiClientLibrary.Wikia.Sites;

namespace WikiClientLibrary.Wikia
{
    /// <summary>
    /// Contains configuration for <see cref="WikiaSite"/>.
    /// </summary>
    public sealed class WikiaSiteOptions : SiteOptions
    {

        /// <summary>Initializes an empty <see cref="WikiaSiteOptions"/> instance.</summary>
        public WikiaSiteOptions()
        {
            
        }

        /// <summary>Initializes a new <see cref="WikiaSiteOptions"/> instance from the information in <see cref="WikiSite"/>.</summary>
        /// <param name="site">The existing wiki site instance.</param>
        /// <exception cref="ArgumentNullException"><paramref name="site"/> is <c>null</c>.</exception>
        public WikiaSiteOptions(WikiSite site)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            ApiEndpoint = site.ApiEndpoint;
            var siteInfo = site.SiteInfo;
            ScriptUrl = MediaWikiHelper.MakeAbsoluteUrl(siteInfo.ServerUrl, siteInfo.ScriptFilePath);
            NirvanaEndPointUrl = MediaWikiHelper.MakeAbsoluteUrl(siteInfo.ServerUrl, "wikia.php");
            WikiaApiRootUrl = MediaWikiHelper.MakeAbsoluteUrl(siteInfo.ServerUrl, "api/v1");
        }

        /// <summary>Initializes a new <see cref="WikiaSiteOptions"/> instance from the root URL of a Wikia site.</summary>
        /// <param name="siteRootUrl">Wikia site root URL, with the ending slash. e.g. <c>http://community.wikia.com/</c>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="siteRootUrl"/> is <c>null</c>.</exception>
        public WikiaSiteOptions(string siteRootUrl)
        {
            if (siteRootUrl == null) throw new ArgumentNullException(nameof(siteRootUrl));
            ApiEndpoint = MediaWikiHelper.MakeAbsoluteUrl(siteRootUrl, "api.php");
            ScriptUrl = MediaWikiHelper.MakeAbsoluteUrl(siteRootUrl, "index.php");
            NirvanaEndPointUrl = MediaWikiHelper.MakeAbsoluteUrl(siteRootUrl, "wikia.php");
            WikiaApiRootUrl = MediaWikiHelper.MakeAbsoluteUrl(siteRootUrl, "api/v1");
        }

        /// <summary>
        /// MediaWiki script URL, as in <see cref="SiteInfo.ScriptFilePath"/>.
        /// </summary>
        /// <remarks>Typically, the value is <c>(Server URL)/index.php</c>.</remarks>
        public string ScriptUrl { get; set; }

        /// <summary>
        /// Wikia Nirvana endpoint URL.
        /// </summary>
        /// <remarks>Typically, the value is <c>(Server URL)/wikia.php</c>.</remarks>
        public string NirvanaEndPointUrl { get; set; }

        /// <summary>
        /// Root URL of Wikia public REST-ful API v1, without the suffixing slash.
        /// </summary>
        /// <remarks>Typically, the value is <c>(Server URL)/api/v1</c>.</remarks>
        public string WikiaApiRootUrl { get; set; }
        
    }
}
