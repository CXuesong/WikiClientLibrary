using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Wikia.Sites
{
    /// <summary>
    /// Represents a node in the WikiVariable API response.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class AppleTouchIconInfo
    {
        private string _Size;

        [JsonProperty("url")]
        public string Url { get; private set; }

        [JsonProperty("size")]
        public string Size
        {
            get { return _Size; }
            private set
            {
                _Size = value;
                if (value != null)
                {
                    var fields = value.Split('x');
                    Width = Convert.ToSingle(fields[0]);
                    Height = Convert.ToSingle(fields[1]);
                }
            }
        }

        public float Width { get; private set; }

        public float Height { get; private set; }

    }

    /// <summary>
    /// Represents a node in the WikiVariable API response.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class SiteLanguageInfo
    {

        [JsonProperty("content")]
        public string ContentLanguage { get; private set; }

        [JsonProperty("contentDir")]
        public string ContentFlowDirection { get; private set; }

        /// <inheritdoc />
        public override string ToString() => ContentLanguage + "," + ContentFlowDirection;
    }

    /// <summary>
    /// Represents a node in the WikiVariable API response.
    /// </summary>
    public class SiteThemeInfo : WikiReadOnlyDictionary
    {

        public string BodyColor => GetStringValue("color-body");

        public string BodyMiddleColor => GetStringValue("color-body-middle");

        public string PageColor => GetStringValue("color-page");

        public string ButtonsColor => GetStringValue("color-buttons");

        public string CommunityHeaderColor => GetStringValue("color-community-header");

        public string LinksColor => GetStringValue("color-links");

        public string HeaderColor => GetStringValue("color-header");

        public string BackgroundImage => GetStringValue("background-image");

        public float BackgroundImageWidth => (float)GetValueDirect("background-image-width");

        public float BackgroundImageHeight => (float)GetValueDirect("background-image-height");

        public bool IsBackgroundDynamic => (bool)GetValueDirect("background-dynamic");

        public string PageOpacity => GetStringValue("page-opacity");

        public string WordmarkFont => GetStringValue("wordmark-font");
    }

    /// <summary>
    /// Represents a node in the WikiVariable API response.
    /// </summary>
    public class TrackingInfo : WikiReadOnlyDictionary
    {

        public string Vertical => GetStringValue("vertical");

        //[JsonProperty("comscore")]
        //public Comscore Comscore { get; private set; }

        //[JsonProperty("nielsen")]
        //public Nielsen Nielsen { get; private set; }

        //[JsonProperty("netzathleten")]
        //public Netzathleten Netzathleten { get; private set; }
    }

    /// <summary>
    /// Represents a node in the WikiVariable API response.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class NavigationItem
    {

        private static readonly NavigationItem[] emptyChildren = { };

        [JsonProperty("text")]
        public string Text { get; private set; }

        /// <summary>
        /// The relative URL of the Page.
        /// </summary>
        /// <remarks>
        /// Absolute URL: obtained from combining relative URL with <see cref="SiteVariableData.BasePath"/> from response.
        /// </remarks>
        [JsonProperty("href")]
        public string Url { get; private set; }

        /// <summary>
        /// Children collection containing article or special pages data.
        /// </summary>
        [JsonProperty("children")]
        public IList<NavigationItem> Children { get; private set; } = emptyChildren;

        /// <inheritdoc />
        public override string ToString() => Text + "(" + Url + ")";

    }

    /// <summary>
    /// Represents a node in the WikiVariable API response.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class SiteHtmlTitleInfo
    {

        [JsonProperty("separator")]
        public string Separator { get; private set; }

        [JsonProperty("parts")]
        public IList<string> Parts { get; private set; }

        /// <inheritdoc />
        public override string ToString()
        {
            if (Parts == null || Parts.Count == 0) return string.Empty;
            return string.Join(Separator, Parts);
        }
    }

    /// <summary>
    /// Represents a node in the WikiVariable API response.
    /// </summary>
    /// <remarks>See
    /// <a href="http://www.wikia.com/api/v1/#!/Mercury/getWikiData_get_0">http://www.wikia.com/api/v1/#!/Mercury/getWikiData_get_0</a>
    /// for Wikia's official documentation for the WikiVariable API response.
    /// </remarks>
    [JsonObject(MemberSerialization.OptIn)]
    public class SiteVariableData
    {

        [JsonProperty("appleTouchIcon")]
        public AppleTouchIconInfo AppleTouchIcon { get; private set; }

        /// <summary>
        /// Current wiki cachebuster value,
        /// </summary>
        [JsonProperty("cacheBuster")]
        public long CacheBuster { get; private set; }

        [JsonProperty("cdnRootUrl")]
        public string CdnRootUrl { get; private set; }

        [JsonProperty("contentNamespaces")]
        public ICollection<int> ContentNamespaceIds { get; private set; }

        [JsonProperty("dbName")]
        public string DatabaseName { get; private set; }

        [JsonProperty("defaultSkin")]
        public string DefaultSkinName { get; private set; }

        [JsonProperty("disableAnonymousEditing")]
        public bool IsAnonymousEditingDisabled { get; private set; }

        [JsonProperty("disableAnonymousUploadForMercury")]
        public bool IsMercuryAnonymousUploadDisabled { get; private set; }

        [JsonProperty("disableMobileSectionEditor")]
        public bool IsMobileSectionEditorDisabled { get; private set; }

        [JsonProperty("enableCommunityData")]
        public bool IsCommunityDataEnabled { get; private set; }

        [JsonProperty("enableDiscussions")]
        public bool IsDiscussionsEnabled { get; private set; }

        [JsonProperty("enableDiscussionsImageUpload")]
        public bool IsDiscussionsImageUploadEnabled { get; private set; }

        [JsonProperty("enableFandomAppSmartBanner")]
        public bool IsFandomAppSmartBannerEnabled { get; private set; }

        [JsonProperty("enableNewAuth")]
        public bool IsNewAuthEnabled { get; private set; }

        [JsonProperty("favicon")]
        public string FavIconUrl { get; private set; }

        /// <summary>Home page URL of FANDOM.</summary>
        /// <seealso cref="MainPageTitle"/>
        [JsonProperty("homepage")]
        public string HomePageUrl { get; private set; }

        [JsonProperty("id")]
        public int Id { get; private set; }

        [JsonProperty("isCoppaWiki")]
        public bool IsCoppaWiki { get; private set; }

        [JsonProperty("isDarkTheme")]
        public bool IsDarkTheme { get; private set; }

        [JsonProperty("language")]
        public SiteLanguageInfo LanguageInfo { get; private set; }

        [JsonProperty("mainPageTitle")]
        public string MainPageTitle { get; private set; }

        [JsonProperty("namespaces")]
        public IDictionary<int, string> NamespaceNames { get; private set; }

        [JsonProperty("siteMessage")]
        public string SiteMessage { get; private set; }

        [JsonProperty("siteName")]
        public string SiteName { get; private set; }

        [JsonProperty("theme")]
        public SiteThemeInfo ThemeInfo { get; private set; }

        [JsonProperty("discussionColorOverride")]
        public string DiscussionColorOverride { get; private set; }

        [JsonProperty("tracking")]
        public TrackingInfo TrackingInfo { get; private set; }

        [JsonProperty("wikiCategories")]
        public IList<string> WikiCategories { get; private set; }

        [JsonProperty("localNav")]
        public IList<NavigationItem> NavigationItems { get; private set; }

        /// <summary>
        /// Gets the site's Wikia hub name (e.g. Gaming, Entertainment, Lifestyle).
        /// </summary>
        [JsonProperty("vertical")]
        public string Vertical { get; private set; }

        [JsonProperty("basePath")]
        public string BasePath { get; private set; }

        [JsonProperty("articlePath")]
        public string ArticlePath { get; private set; }

        [JsonProperty("image")]
        public string Image { get; private set; }

        [JsonProperty("specialRobotPolicy")]
        public object SpecialRobotPolicy { get; private set; }

        [JsonProperty("htmlTitle")]
        public SiteHtmlTitleInfo HtmlTitle { get; private set; }

        /// <inheritdoc />
        public override string ToString() => $"[{Id}]{SiteName}";
    }
}
