using System;

namespace WikiClientLibrary.Pages.Parsing
{

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
