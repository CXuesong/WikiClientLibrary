using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;

namespace WikiClientLibrary.Sites
{
    partial class WikiSite
    {

        private IDictionary<string, object> BuildParsingParams(ParsingOptions options)
        {
            var p = new Dictionary<string, object>
            {
                {"action", "parse"},
                {"prop", "text|langlinks|categories|sections|revid|displaytitle|properties"},
                {"disabletoc", (options & ParsingOptions.DisableToc) == ParsingOptions.DisableToc},
                {"preview", (options & ParsingOptions.Preview) == ParsingOptions.Preview},
                {"sectionpreview", (options & ParsingOptions.SectionPreview) == ParsingOptions.SectionPreview},
                {"redirects", (options & ParsingOptions.ResolveRedirects) == ParsingOptions.ResolveRedirects},
                {"mobileformat", (options & ParsingOptions.MobileFormat) == ParsingOptions.MobileFormat},
                {"noimages", (options & ParsingOptions.NoImages) == ParsingOptions.NoImages},
                {"effectivelanglinks", (options & ParsingOptions.EffectiveLanguageLinks) == ParsingOptions.EffectiveLanguageLinks},
            };
            if ((options & ParsingOptions.TranscludedPages) == ParsingOptions.TranscludedPages)
                p["prop"] += "|templates";
            if ((options & ParsingOptions.LimitReport) == ParsingOptions.LimitReport)
                p["prop"] += "|limitreportdata";
            if ((options & ParsingOptions.DisableLimitReport) == ParsingOptions.DisableLimitReport)
            {
                if (SiteInfo.Version >= new Version("1.26"))
                    p["disablelimitreport"] = true;
                else
                    p["disablepp"] = true;
            }
            return p;
        }

        /// <summary>
        /// Parsing the specific page, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="title">Title of the page to be parsed.</param>
        /// <exception cref="ArgumentNullException"><paramref name="title"/> is <c>null</c>.</exception>
        /// <remarks>This overload will not follow the redirects.</remarks>
        public Task<ParsedContentInfo> ParsePageAsync(string title)
        {
            return ParsePageAsync(title, null, ParsingOptions.None, CancellationToken.None);
        }

        /// <summary>
        /// Parsing the specific page, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="title">Title of the page to be parsed.</param>
        /// <param name="options">Options for parsing.</param>
        /// <exception cref="ArgumentNullException"><paramref name="title"/> is <c>null</c>.</exception>
        public Task<ParsedContentInfo> ParsePageAsync(string title, ParsingOptions options)
        {
            return ParsePageAsync(title, null, options, CancellationToken.None);
        }

        /// <summary>
        /// Parsing the specific page, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="title">Title of the page to be parsed.</param>
        /// <param name="options">Options for parsing.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <exception cref="ArgumentNullException"><paramref name="title"/> is <c>null</c>.</exception>
        public Task<ParsedContentInfo> ParsePageAsync(string title, ParsingOptions options, CancellationToken cancellationToken)
        {
            return ParsePageAsync(title, null, options, CancellationToken.None);
        }

        /// <summary>
        /// Parsing the specific page, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="title">Title of the page to be parsed.</param>
        /// <param name="lang">The language (variant) used to render the content. E.g. <c>zh-cn</c>, <c>zh-tw</c>. specify <c>content</c> to use this wiki's content language.</param>
        /// <param name="options">Options for parsing.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <exception cref="ArgumentNullException"><paramref name="title"/> is <c>null</c>.</exception>
        public async Task<ParsedContentInfo> ParsePageAsync(string title, string lang, ParsingOptions options, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(title)) throw new ArgumentNullException(nameof(title));
            var p = BuildParsingParams(options);
            p["page"] = title;
            p["uselang"] = lang;
            var jobj = await GetJsonAsync(new WikiFormRequestMessage(p), cancellationToken);
            var parsed = ((JObject)jobj["parse"]).ToObject<ParsedContentInfo>(Utility.WikiJsonSerializer);
            return parsed;
        }

        /// <summary>
        /// Parsing the specific page, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="id">Id of the page to be parsed.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero or negative.</exception>
        /// <remarks>This overload will not follow the redirects.</remarks>
        public Task<ParsedContentInfo> ParsePageAsync(int id)
        {
            return ParsePageAsync(id, null, ParsingOptions.None, CancellationToken.None);
        }

        /// <summary>
        /// Parsing the specific page, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="id">Id of the page to be parsed.</param>
        /// <param name="options">Options for parsing.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero or negative.</exception>
        public Task<ParsedContentInfo> ParsePageAsync(int id, ParsingOptions options)
        {
            return ParsePageAsync(id, null, options, CancellationToken.None);
        }

        /// <summary>
        /// Parsing the specific page, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="id">Id of the page to be parsed.</param>
        /// <param name="options">Options for parsing.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero or negative.</exception>
        public Task<ParsedContentInfo> ParsePageAsync(int id, ParsingOptions options, CancellationToken cancellationToken)
        {
            return ParsePageAsync(id, null, options, CancellationToken.None);
        }

        /// <summary>
        /// Parsing the specific page, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="id">Id of the page to be parsed.</param>
        /// <param name="lang">The language (variant) used to render the content. E.g. <c>zh-cn</c>, <c>zh-tw</c>. specify <c>content</c> to use this wiki's content language.</param>
        /// <param name="options">Options for parsing.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero or negative.</exception>
        public async Task<ParsedContentInfo> ParsePageAsync(int id, string lang, ParsingOptions options, CancellationToken cancellationToken)
        {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            var p = BuildParsingParams(options);
            p["pageid"] = id;
            p["uselang"] = lang;
            var jobj = await GetJsonAsync(new WikiFormRequestMessage(p), cancellationToken);
            var parsed = ((JObject)jobj["parse"]).ToObject<ParsedContentInfo>(Utility.WikiJsonSerializer);
            return parsed;
        }

        /// <summary>
        /// Parsing the specific page revision, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="revId">Id of the revision to be parsed.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="revId"/> is zero or negative.</exception>
        public Task<ParsedContentInfo> ParseRevisionAsync(int revId)
        {
            return ParseRevisionAsync(revId, null, ParsingOptions.None, CancellationToken.None);
        }

        /// <summary>
        /// Parsing the specific page revision, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="revId">Id of the revision to be parsed.</param>
        /// <param name="options">Options for parsing.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="revId"/> is zero or negative.</exception>
        public Task<ParsedContentInfo> ParseRevisionAsync(int revId, ParsingOptions options)
        {
            return ParseRevisionAsync(revId, null, options, CancellationToken.None);
        }

        /// <summary>
        /// Parsing the specific page revision, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="revId">Id of the revision to be parsed.</param>
        /// <param name="options">Options for parsing.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="revId"/> is zero or negative.</exception>
        public Task<ParsedContentInfo> ParseRevisionAsync(int revId, ParsingOptions options, CancellationToken cancellationToken)
        {
            return ParseRevisionAsync(revId, null, options, CancellationToken.None);
        }

        /// <summary>
        /// Parsing the specific page revision, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="revId">Id of the revision to be parsed.</param>
        /// <param name="lang">The language (variant) used to render the content. E.g. <c>zh-cn</c>, <c>zh-tw</c>. specify <c>content</c> to use this wiki's content language.</param>
        /// <param name="options">Options for parsing.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="revId"/> is zero or negative.</exception>
        public async Task<ParsedContentInfo> ParseRevisionAsync(int revId, string lang, ParsingOptions options, CancellationToken cancellationToken)
        {
            if (revId <= 0) throw new ArgumentOutOfRangeException(nameof(revId));
            var p = BuildParsingParams(options);
            p["oldid"] = revId;
            var jobj = await GetJsonAsync(new WikiFormRequestMessage(p), cancellationToken);
            var parsed = ((JObject)jobj["parse"]).ToObject<ParsedContentInfo>(Utility.WikiJsonSerializer);
            return parsed;
        }

        /// <summary>
        /// Parsing the specific page content and/or summary, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="content">The content to parse.</param>
        /// <param name="summary">The summary to parse.</param>
        /// <param name="title">Act like the wikitext is on this page.
        /// This only really matters when parsing links to the page itself or subpages,
        /// or when using magic words like {{PAGENAME}}.
        /// If <c>null</c> is given, the default value "API" will be used.</param>
        /// <remarks>If both <paramref name="title"/> is <c>null</c>, the content model will be assumed as wikitext.</remarks>
        /// <param name="options">Options for parsing.</param>
        /// <remarks>The content model will be inferred from <paramref name="title"/>.</remarks>
        /// <exception cref="ArgumentException">Both <paramref name="content"/> and <paramref name="summary"/> is <c>null</c>.</exception>
        public Task<ParsedContentInfo> ParseContentAsync(string content, string summary, string title, ParsingOptions options)
        {
            return ParseContentAsync(content, summary, title, null, null, options, CancellationToken.None);
        }

        /// <summary>
        /// Parsing the specific page content and/or summary, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="content">The content to parse.</param>
        /// <param name="summary">The summary to parse. Can be <c>null</c>.</param>
        /// <param name="title">Act like the wikitext is on this page.
        ///     This only really matters when parsing links to the page itself or subpages,
        ///     or when using magic words like {{PAGENAME}}.
        ///     If <c>null</c> is given, the default value "API" will be used.</param>
        /// <param name="contentModel">The content model name of the text specified in <paramref name="content"/>. <c>null</c> makes the server to infer content model from <paramref name="title"/>.</param>
        /// <param name="options">Options for parsing.</param>
        /// <remarks>If both <paramref name="title"/> and <paramref name="contentModel"/> is <c>null</c>, the content model will be assumed as wikitext.</remarks>
        /// <exception cref="ArgumentException">Both <paramref name="content"/> and <paramref name="summary"/> is <c>null</c>.</exception>
        public Task<ParsedContentInfo> ParseContentAsync(string content, string summary, string title,
            string contentModel, ParsingOptions options)
        {
            return ParseContentAsync(content, summary, title, contentModel, null, options, CancellationToken.None);
        }

        /// <summary>
        /// Parsing the specific page content and/or summary, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="content">The content to parse.</param>
        /// <param name="summary">The summary to parse. Can be <c>null</c>.</param>
        /// <param name="title">Act like the wikitext is on this page.
        ///     This only really matters when parsing links to the page itself or subpages,
        ///     or when using magic words like {{PAGENAME}}.
        ///     If <c>null</c> is given, the default value "API" will be used.</param>
        /// <param name="contentModel">The content model name of the text specified in <paramref name="content"/>. <c>null</c> makes the server to infer content model from <paramref name="title"/>.</param>
        /// <param name="options">Options for parsing.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <remarks>If both <paramref name="title"/> and <paramref name="contentModel"/> is <c>null</c>, the content model will be assumed as wikitext.</remarks>
        /// <exception cref="ArgumentException">Both <paramref name="content"/> and <paramref name="summary"/> is <c>null</c>.</exception>
        public Task<ParsedContentInfo> ParseContentAsync(string content, string summary, string title,
            string contentModel, ParsingOptions options, CancellationToken cancellationToken)
        {
            return ParseContentAsync(content, summary, title, contentModel, null, options, CancellationToken.None);
        }

        /// <summary>
        /// Parsing the specific page content and/or summary, gets HTML and more information. (MediaWiki 1.12)
        /// </summary>
        /// <param name="content">The content to parse.</param>
        /// <param name="summary">The summary to parse. Can be <c>null</c>.</param>
        /// <param name="title">Act like the wikitext is on this page.
        ///     This only really matters when parsing links to the page itself or subpages,
        ///     or when using magic words like {{PAGENAME}}.
        ///     If <c>null</c> is given, the default value "API" will be used.</param>
        /// <param name="contentModel">The content model name of the text specified in <paramref name="content"/>. <c>null</c> makes the server to infer content model from <paramref name="title"/>.</param>
        /// <param name="lang">The language (variant) used to render the content. E.g. <c>zh-cn</c>, <c>zh-tw</c>. specify <c>content</c> to use this wiki's content language.</param>
        /// <param name="options">Options for parsing.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <remarks>If both <paramref name="title"/> and <paramref name="contentModel"/> is <c>null</c>, the content model will be assumed as wikitext.</remarks>
        /// <exception cref="ArgumentException">Both <paramref name="content"/> and <paramref name="summary"/> is <c>null</c>.</exception>
        public async Task<ParsedContentInfo> ParseContentAsync(string content, string summary, string title,
            string contentModel, string lang, ParsingOptions options, CancellationToken cancellationToken)
        {
            if (content == null && summary == null) throw new ArgumentException(nameof(content));
            var p = BuildParsingParams(options);
            p["text"] = content;
            p["summary"] = summary;
            p["title"] = title;
            p["uselang"] = lang;
            var jobj = await GetJsonAsync(new WikiFormRequestMessage(p), cancellationToken);
            var parsed = ((JObject)jobj["parse"]).ToObject<ParsedContentInfo>(Utility.WikiJsonSerializer);
            return parsed;
        }

    }

    /// <summary>
    /// Options for page or content parsing.
    /// </summary>
    [Flags]
    public enum ParsingOptions
    {
        None = 0,
        /// <summary>
        /// When parsing by page title or page id, returns the target page when meeting redirects.
        /// </summary>
        ResolveRedirects = 1,
        /// <summary>
        /// Disable table of contents in output. (1.23+)
        /// </summary>
        DisableToc = 2,
        /// <summary>
        /// Parse in preview mode. (1.22+)
        /// </summary>
        Preview = 4,
        /// <summary>
        /// Parse in section preview mode (enables preview mode too). (1.22+)
        /// </summary>
        SectionPreview = 8,
        /// <summary>
        /// Return parse output in a format suitable for mobile devices. (?)
        /// </summary>
        MobileFormat = 16,
        /// <summary>
        /// Disable images in mobile output. (?)
        /// </summary>
        NoImages = 0x20,
        /// <summary>
        /// Gives the structured limit report. (1.23+)
        /// This flag fills <see cref="ParsedContentInfo.ParserLimitReports"/>.
        /// </summary>
        LimitReport = 0x40,
        /// <summary>
        /// Omit the limit report ("NewPP limit report") from the parser output. (1.17+, disablepp; 1.23+, disablelimitreport)
        /// <see cref="ParsedContentInfo.ParserLimitReports"/> will be empty if both this flag and <see cref="LimitReport"/> is set.
        /// </summary>
        /// <remarks>By default, the limit report will be included as comment in the parsed HTML content.
        /// This flag can supress such output.</remarks>
        DisableLimitReport = 0x80,
        /// <summary>
        /// Includes language links supplied by extensions, in addition to the links specified on the page. (1.22+)
        /// </summary>
        EffectiveLanguageLinks = 0x100,
        /// <summary>
        /// Gives the templates and other transcluded pages/modules in the parsed wikitext.
        /// </summary>
        TranscludedPages = 0x200,
    }

}
