using System;
using System.Collections.Generic;
using System.Text;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Wikia
{
    /// <summary>
    /// Contains configuration for <see cref="WikiaSite"/>.
    /// </summary>
    public sealed class WikiaSiteOptions
    {
        /// <summary>Initializes a new <see cref="WikiaSiteOptions"/> instance from <see cref="SiteInfo"/>.</summary>
        /// <param name="siteInfo">The retrieved site info.</param>
        /// <exception cref="ArgumentNullException"><paramref name="siteInfo"/> is <c>null</c>.</exception>
        public WikiaSiteOptions(SiteInfo siteInfo)
        {
            if (siteInfo == null) throw new ArgumentNullException(nameof(siteInfo));
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
            ScriptUrl = MediaWikiHelper.MakeAbsoluteUrl(siteRootUrl, "index.php");
            NirvanaEndPointUrl = MediaWikiHelper.MakeAbsoluteUrl(siteRootUrl, "wikia.php");
            WikiaApiRootUrl = MediaWikiHelper.MakeAbsoluteUrl(siteRootUrl, "api/v1");
        }

        /// <summary>Initializes a new <see cref="WikiaSiteOptions"/> instance from API end point URLs.</summary>
        /// <param name="scriptUrl">MediaWiki script URL, as in <see cref="SiteInfo.ScriptFilePath"/>. Typically this is <c>(Server URL)/index.php</c>.</param>
        /// <param name="nirvanaEndPointUrl">Wikia Nirvana end point URL. Typically this is <c>(Server URL)/wikia.php</c>.</param>
        /// <param name="wikiaPublicEndPointUrl">Wikia public REST-ful API v1 end point URL, without the suffixing slash. Typically this is <c>(Server URL)/api/v1</c>.</param>
        /// <exception cref="ArgumentNullException">Either one of the arguments is <c>null</c>.</exception>
        public WikiaSiteOptions(string scriptUrl, string nirvanaEndPointUrl, string wikiaPublicEndPointUrl)
        {
            ScriptUrl = scriptUrl ?? throw new ArgumentNullException(nameof(scriptUrl));
            NirvanaEndPointUrl = nirvanaEndPointUrl ?? throw new ArgumentNullException(nameof(nirvanaEndPointUrl));
            WikiaApiRootUrl = wikiaPublicEndPointUrl ?? throw new ArgumentNullException(nameof(wikiaPublicEndPointUrl));
        }

        /// <summary>
        /// MediaWiki script URL, as in <see cref="SiteInfo.ScriptFilePath"/>.
        /// </summary>
        /// <remarks>Typically, the value is <c>(Server URL)/index.php</c>.</remarks>
        public string ScriptUrl { get; }

        /// <summary>
        /// Wikia Nirvana endpoint URL.
        /// </summary>
        /// <remarks>Typically, the value is <c>(Server URL)/wikia.php</c>.</remarks>
        public string NirvanaEndPointUrl { get; }

        /// <summary>
        /// Root URL of Wikia public REST-ful API v1, without the suffixing slash.
        /// </summary>
        /// <remarks>Typically, the value is <c>(Server URL)/api/v1</c>.</remarks>
        public string WikiaApiRootUrl { get; }
        
    }
}
