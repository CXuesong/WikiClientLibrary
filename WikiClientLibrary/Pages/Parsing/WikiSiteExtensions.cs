using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Pages.Parsing;

/// <summary>
/// Provides extension methods to <see cref="WikiSite"/> for parsing wikitext content into HTML.
/// </summary>
public static class WikiSiteExtensions
{

    private static IDictionary<string, object?> BuildParsingParams(WikiSite site, ParsingOptions options)
    {
        var p = new Dictionary<string, object?>
        {
            { "action", "parse" },
            { "prop", "text|langlinks|categories|sections|revid|displaytitle|properties" },
            { "disabletoc", (options & ParsingOptions.DisableToc) == ParsingOptions.DisableToc },
            { "disableeditsection", (options & ParsingOptions.DisableEditSection) == ParsingOptions.DisableEditSection },
            { "disabletidy", (options & ParsingOptions.DisableTidy) == ParsingOptions.DisableTidy },
            { "preview", (options & ParsingOptions.Preview) == ParsingOptions.Preview },
            { "sectionpreview", (options & ParsingOptions.SectionPreview) == ParsingOptions.SectionPreview },
            { "redirects", (options & ParsingOptions.ResolveRedirects) == ParsingOptions.ResolveRedirects },
            { "mobileformat", (options & ParsingOptions.MobileFormat) == ParsingOptions.MobileFormat },
            { "noimages", (options & ParsingOptions.NoImages) == ParsingOptions.NoImages },
        };
        if ((options & ParsingOptions.EffectiveLanguageLinks) == ParsingOptions.EffectiveLanguageLinks)
        {
            if (site.SiteInfo.Version.Above(1, 30, 0))
                // https://github.com/wikimedia/mediawiki/commit/df5b122641bf047d8f5834d1f0c219769c3593c2
                p["useskin"] = "apioutput";
            else
                p["effectivelanglinks"] = "true";
        }
        if ((options & ParsingOptions.TranscludedPages) == ParsingOptions.TranscludedPages)
            p["prop"] += "|templates";
        if ((options & ParsingOptions.LimitReport) == ParsingOptions.LimitReport)
            p["prop"] += "|limitreportdata";
        if ((options & ParsingOptions.DisableLimitReport) == ParsingOptions.DisableLimitReport)
        {
            if (site.SiteInfo.Version >= new MediaWikiVersion(1, 26))
                p["disablelimitreport"] = true;
            else
                p["disablepp"] = true;
        }
        return p;
    }

    /// <inheritdoc cref="ParsePageAsync(WikiSite,string,string,ParsingOptions,CancellationToken)"/>
    /// <remarks>This overload will not follow the redirects.</remarks>
    public static Task<ParsedContentInfo> ParsePageAsync(this WikiSite site, string title)
    {
        return ParsePageAsync(site, title, null, ParsingOptions.None, CancellationToken.None);
    }

    /// <inheritdoc cref="ParsePageAsync(WikiSite,string,string,ParsingOptions,CancellationToken)"/>
    public static Task<ParsedContentInfo> ParsePageAsync(this WikiSite site, string title, ParsingOptions options)
    {
        return ParsePageAsync(site, title, null, options, CancellationToken.None);
    }

    /// <inheritdoc cref="ParsePageAsync(WikiSite,string,string,ParsingOptions,CancellationToken)"/>
    public static Task<ParsedContentInfo> ParsePageAsync(this WikiSite site, string title, ParsingOptions options,
        CancellationToken cancellationToken)
    {
        return ParsePageAsync(site, title, null, options, cancellationToken);
    }

    /// <summary>
    /// Parsing the specific page, gets HTML and more information. (MediaWiki 1.12)
    /// </summary>
    /// <param name="site">The MediaWiki site to execute the request on.</param>
    /// <param name="title">Title of the page to be parsed.</param>
    /// <param name="lang">The language (variant) used to render the content. E.g. <c>"zh-cn"</c>, <c>"zh-tw"</c>. specify <c>"content"</c> to use this wiki's content language.</param>
    /// <param name="options">Options for parsing.</param>
    /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
    /// <exception cref="ArgumentNullException"><paramref name="title"/> is <c>null</c>.</exception>
    public static async Task<ParsedContentInfo> ParsePageAsync(this WikiSite site, string title, string? lang, ParsingOptions options,
        CancellationToken cancellationToken)
    {
        if (site == null) throw new ArgumentNullException(nameof(site));
        if (string.IsNullOrEmpty(title)) throw new ArgumentNullException(nameof(title));
        var p = BuildParsingParams(site, options);
        p["page"] = title;
        p["uselang"] = lang;
        var jobj = await site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(p), cancellationToken);
        var parsed = ((JObject)jobj["parse"]).ToObject<ParsedContentInfo>(Utility.WikiJsonSerializer);
        return parsed;
    }

    /// <inheritdoc cref="ParsePageAsync(WikiSite,string,string,ParsingOptions,CancellationToken)"/>
    /// <remarks>This overload will not follow the redirects.</remarks>
    public static Task<ParsedContentInfo> ParsePageAsync(this WikiSite site, int id)
    {
        return ParsePageAsync(site, id, null, ParsingOptions.None, CancellationToken.None);
    }

    /// <inheritdoc cref="ParsePageAsync(WikiSite,string,string,ParsingOptions,CancellationToken)"/>
    public static Task<ParsedContentInfo> ParsePageAsync(this WikiSite site, int id, ParsingOptions options)
    {
        return ParsePageAsync(site, id, null, options, CancellationToken.None);
    }

    /// <inheritdoc cref="ParsePageAsync(WikiSite,string,string,ParsingOptions,CancellationToken)"/>
    public static Task<ParsedContentInfo> ParsePageAsync(this WikiSite site, int id, ParsingOptions options,
        CancellationToken cancellationToken)
    {
        return ParsePageAsync(site, id, null, options, cancellationToken);
    }

    /// <summary>
    /// Parsing the specific page, gets HTML and more information. (MediaWiki 1.12)
    /// </summary>
    /// <param name="site">The MediaWiki site to execute the request on.</param>
    /// <param name="id">Id of the page to be parsed.</param>
    /// <param name="lang">The language (variant) used to render the content. E.g. <c>"zh-cn"</c>, <c>"zh-tw"</c>. specify <c>content</c> to use this wiki's content language.</param>
    /// <param name="options">Options for parsing.</param>
    /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero or negative.</exception>
    public static async Task<ParsedContentInfo> ParsePageAsync(this WikiSite site, int id, string? lang, ParsingOptions options,
        CancellationToken cancellationToken)
    {
        if (site == null) throw new ArgumentNullException(nameof(site));
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        var p = BuildParsingParams(site, options);
        p["pageid"] = id;
        p["uselang"] = lang;
        var jobj = await site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(p), cancellationToken);
        var parsed = ((JObject)jobj["parse"]).ToObject<ParsedContentInfo>(Utility.WikiJsonSerializer);
        return parsed;
    }

    /// <inheritdoc cref="ParseRevisionAsync(WikiSite,long,string,ParsingOptions,CancellationToken)"/>
    public static Task<ParsedContentInfo> ParseRevisionAsync(this WikiSite site, long revId)
    {
        return ParseRevisionAsync(site, revId, null, ParsingOptions.None, CancellationToken.None);
    }

    /// <inheritdoc cref="ParseRevisionAsync(WikiSite,long,string,ParsingOptions,CancellationToken)"/>
    public static Task<ParsedContentInfo> ParseRevisionAsync(this WikiSite site, long revId, ParsingOptions options)
    {
        return ParseRevisionAsync(site, revId, null, options, CancellationToken.None);
    }

    /// <inheritdoc cref="ParseRevisionAsync(WikiSite,long,string,ParsingOptions,CancellationToken)"/>
    public static Task<ParsedContentInfo> ParseRevisionAsync(this WikiSite site, long revId, ParsingOptions options,
        CancellationToken cancellationToken)
    {
        return ParseRevisionAsync(site, revId, null, options, CancellationToken.None);
    }

    /// <summary>
    /// Parsing the specific page revision, gets HTML and more information. (MediaWiki 1.12)
    /// </summary>
    /// <param name="site">The MediaWiki site to execute the request on.</param>
    /// <param name="revId">Id of the revision to be parsed.</param>
    /// <param name="lang">The language (variant) used to render the content. E.g. <c>"zh-cn"</c>, <c>"zh-tw"</c>. specify <c>content</c> to use this wiki's content language.</param>
    /// <param name="options">Options for parsing.</param>
    /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="revId"/> is zero or negative.</exception>
    public static async Task<ParsedContentInfo> ParseRevisionAsync(this WikiSite site, long revId, string? lang, ParsingOptions options,
        CancellationToken cancellationToken)
    {
        if (site == null) throw new ArgumentNullException(nameof(site));
        if (revId <= 0) throw new ArgumentOutOfRangeException(nameof(revId));
        var p = BuildParsingParams(site, options);
        p["oldid"] = revId;
        p["uselang"] = lang;
        var jobj = await site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(p), cancellationToken);
        var parsed = ((JObject)jobj["parse"]).ToObject<ParsedContentInfo>(Utility.WikiJsonSerializer);
        return parsed;
    }

    /// <inheritdoc cref="ParseContentAsync(WikiSite,string,string,string,string,string,ParsingOptions,CancellationToken)"/>
    public static Task<ParsedContentInfo> ParseContentAsync(this WikiSite site, string? content, string? summary, string? title,
        ParsingOptions options)
    {
        return ParseContentAsync(site, content, summary, title, null, null, options, CancellationToken.None);
    }

    /// <inheritdoc cref="ParseContentAsync(WikiSite,string,string,string,string,string,ParsingOptions,CancellationToken)"/>
    public static Task<ParsedContentInfo> ParseContentAsync(this WikiSite site, string? content, string? summary, string? title,
        string contentModel, ParsingOptions options)
    {
        return ParseContentAsync(site, content, summary, title, contentModel, null, options, CancellationToken.None);
    }

    /// <inheritdoc cref="ParseContentAsync(WikiSite,string,string,string,string,string,ParsingOptions,CancellationToken)"/>
    public static Task<ParsedContentInfo> ParseContentAsync(this WikiSite site, string? content, string? summary, string? title,
        string contentModel, ParsingOptions options, CancellationToken cancellationToken)
    {
        return ParseContentAsync(site, content, summary, title, contentModel, null, options, cancellationToken);
    }

    /// <summary>
    /// Parsing the specific page content and/or summary, gets HTML and more information. (MediaWiki 1.12)
    /// </summary>
    /// <param name="site">The MediaWiki site to execute the request on.</param>
    /// <param name="content">The content to parse.</param>
    /// <param name="summary">The summary to parse. Can be <c>null</c>.</param>
    /// <param name="title">Act like the wikitext is on this page.
    ///     This only really matters when parsing links to the page itself or subpages,
    ///     or when using magic words like <c>{{PAGENAME}}</c>.
    ///     If <c>null</c> is given, the default value <c>"API"</c> will be used.</param>
    /// <param name="contentModel">The content model name of the text specified in <paramref name="content"/>. <c>null</c> makes the server to infer content model from <paramref name="title"/>.</param>
    /// <param name="lang">The language (variant) used to render the content. E.g. <c>"zh-cn"</c>, <c>"zh-tw"</c>. specify <c>content</c> to use this wiki's content language.</param>
    /// <param name="options">Options for parsing.</param>
    /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
    /// <remarks>If both <paramref name="title"/> and <paramref name="contentModel"/> is <c>null</c>, the content model will be assumed as wikitext.</remarks>
    /// <exception cref="ArgumentException">Both <paramref name="content"/> and <paramref name="summary"/> is <c>null</c>.</exception>
    public static async Task<ParsedContentInfo> ParseContentAsync(this WikiSite site, string? content, string? summary, string? title,
        string? contentModel, string? lang, ParsingOptions options, CancellationToken cancellationToken)
    {
        if (content == null && summary == null) throw new ArgumentException(nameof(content));
        var p = BuildParsingParams(site, options);
        p["text"] = content;
        p["summary"] = summary;
        p["title"] = title;
        p["uselang"] = lang;
        p["contentmodel"] = contentModel;
        var jobj = await site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(p), cancellationToken);
        var parsed = ((JObject)jobj["parse"]).ToObject<ParsedContentInfo>(Utility.WikiJsonSerializer);
        return parsed;
    }

}
