using System.Globalization;
using System.Text.Json.Serialization;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Wikia.Sites;

/// <summary>
/// Represents a node in the WikiVariable API response.
/// </summary>
[JsonContract]
public sealed class AppleTouchIconInfo
{

    private string _Size;

    [JsonPropertyName("url")]
    public string Url { get; init; }

    [JsonPropertyName("size")]
    public string Size
    {
        get { return _Size; }
        init
        {
            _Size = value;
            if (value != null)
            {
                var fields = value.Split('x');
                Width = Convert.ToSingle(fields[0], CultureInfo.InvariantCulture);
                Height = Convert.ToSingle(fields[1], CultureInfo.InvariantCulture);
            }
        }
    }

    public float Width { get; init; }

    public float Height { get; init; }

}

/// <summary>
/// Represents a node in the WikiVariable API response.
/// </summary>
[JsonContract]
public sealed class SiteLanguageInfo
{

    [JsonPropertyName("content")]
    public string ContentLanguage { get; init; }

    [JsonPropertyName("contentDir")]
    public string ContentFlowDirection { get; init; }

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

    public float BackgroundImageWidth => (float)this["background-image-width"].GetDouble();

    public float BackgroundImageHeight => (float)this["background-image-height"].GetDouble();

    public bool IsBackgroundDynamic => GetBooleanValue("background-dynamic");

    public string PageOpacity => GetStringValue("page-opacity");

    public string WordmarkFont => GetStringValue("wordmark-font");

}

/// <summary>
/// Represents a node in the WikiVariable API response.
/// </summary>
public class TrackingInfo : WikiReadOnlyDictionary
{

    public string Vertical => GetStringValue("vertical");

    //[JsonPropertyName("comscore")]
    //public Comscore Comscore { get; init; }

    //[JsonPropertyName("nielsen")]
    //public Nielsen Nielsen { get; init; }

    //[JsonPropertyName("netzathleten")]
    //public Netzathleten Netzathleten { get; init; }

}

/// <summary>
/// Represents a node in the WikiVariable API response.
/// </summary>
[JsonContract]
public sealed class NavigationItem
{

    [JsonPropertyName("text")]
    public string Text { get; init; }

    /// <summary>
    /// The relative URL of the Page.
    /// </summary>
    /// <remarks>
    /// Absolute URL: obtained from combining relative URL with <see cref="SiteVariableData.BasePath"/> from response.
    /// </remarks>
    [JsonPropertyName("href")]
    public string Url { get; init; }

    /// <summary>
    /// Children collection containing article or special pages data.
    /// </summary>
    [JsonPropertyName("children")]
    public IList<NavigationItem> Children { get; init; } = Array.Empty<NavigationItem>();

    /// <inheritdoc />
    public override string ToString() => Text + "(" + Url + ")";

}

/// <summary>
/// Represents a node in the WikiVariable API response.
/// </summary>
[JsonContract]
public sealed class SiteHtmlTitleInfo
{

    [JsonPropertyName("separator")]
    public string Separator { get; init; }

    [JsonPropertyName("parts")]
    public IList<string> Parts { get; init; }

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
[JsonContract]
public sealed class SiteVariableData
{

    private static readonly ICollection<int> fallbackContentNamespaceIds = Array.AsReadOnly(new[] { 0 });

    [JsonPropertyName("appleTouchIcon")]
    public AppleTouchIconInfo AppleTouchIcon { get; init; }

    /// <summary>
    /// Current wiki cachebuster value.
    /// </summary>
    [JsonPropertyName("cacheBuster")]
    [Obsolete("cacheBuster is not present in latest Wikia sites anymore. This property will be removed.")]
    public long CacheBuster { get; init; }

    [JsonPropertyName("cdnRootUrl")]
    public string CdnRootUrl { get; init; }

    [Obsolete("contentNamespaces is not present in latest Wikia sites anymore. This property will be removed.")]
    public ICollection<int> ContentNamespaceIds => fallbackContentNamespaceIds;

    [JsonPropertyName("dbName")]
    public string DatabaseName { get; init; }

    [JsonPropertyName("defaultSkin")]
    [Obsolete("defaultSkin is not present in latest Wikia sites anymore. This property will be removed.")]
    public string DefaultSkinName { get; init; }

    [JsonPropertyName("disableAnonymousEditing")]
    public bool IsAnonymousEditingDisabled { get; init; }

    [JsonPropertyName("disableAnonymousUploadForMercury")]
    public bool IsMercuryAnonymousUploadDisabled { get; init; }

    [JsonPropertyName("disableMobileSectionEditor")]
    public bool IsMobileSectionEditorDisabled { get; init; }

    [JsonPropertyName("enableCommunityData")]
    public bool IsCommunityDataEnabled { get; init; }

    [JsonPropertyName("enableDiscussions")]
    public bool IsDiscussionsEnabled { get; init; }

    [JsonPropertyName("enableDiscussionsImageUpload")]
    public bool IsDiscussionsImageUploadEnabled { get; init; }

    [JsonPropertyName("enableFandomAppSmartBanner")]
    public bool IsFandomAppSmartBannerEnabled { get; init; }

    [JsonPropertyName("enableNewAuth")]
    public bool IsNewAuthEnabled { get; init; }

    [JsonPropertyName("favicon")]
    public string FavIconUrl { get; init; }

    /// <summary>Home page URL of FANDOM.</summary>
    /// <seealso cref="MainPageTitle"/>
    [JsonPropertyName("homepage")]
    public string HomePageUrl { get; init; }

    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("isCoppaWiki")]
    public bool IsCoppaWiki { get; init; }

    [JsonPropertyName("isDarkTheme")]
    public bool IsDarkTheme { get; init; }

    [JsonPropertyName("language")]
    public SiteLanguageInfo LanguageInfo { get; init; }

    [JsonPropertyName("mainPageTitle")]
    public string MainPageTitle { get; init; }

    [JsonPropertyName("namespaces")]
    public IDictionary<int, string> NamespaceNames { get; init; }

    [JsonPropertyName("siteMessage")]
    public string SiteMessage { get; init; }

    [JsonPropertyName("siteName")]
    public string SiteName { get; init; }

    [JsonPropertyName("theme")]
    public SiteThemeInfo ThemeInfo { get; init; }

    [JsonPropertyName("discussionColorOverride")]
    public string DiscussionColorOverride { get; init; }

    [JsonPropertyName("tracking")]
    public TrackingInfo TrackingInfo { get; init; }

    [JsonPropertyName("wikiCategories")]
    public IList<string> WikiCategories { get; init; }

    [JsonPropertyName("localNav")]
    public IList<NavigationItem> NavigationItems { get; init; }

    /// <summary>
    /// Gets the site's Wikia hub name (e.g. Gaming, Entertainment, Lifestyle).
    /// </summary>
    [JsonPropertyName("vertical")]
    public string Vertical { get; init; }

    [JsonPropertyName("basePath")]
    public string BasePath { get; init; }

    [JsonPropertyName("articlePath")]
    public string ArticlePath { get; init; }

    [JsonPropertyName("image")]
    public string Image { get; init; }

    [JsonPropertyName("specialRobotPolicy")]
    public object SpecialRobotPolicy { get; init; }

    [JsonPropertyName("htmlTitle")]
    public SiteHtmlTitleInfo HtmlTitle { get; init; }

    /// <inheritdoc />
    public override string ToString() => $"[{Id}]{SiteName}";

}
