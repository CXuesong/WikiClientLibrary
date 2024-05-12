namespace WikiClientLibrary.Pages.Parsing;

/// <summary>
/// Options for page or content parsing.
/// </summary>
[Flags]
public enum ParsingOptions
{
    /// <summary>
    /// No parsing options.
    /// </summary>
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
    /// Disable edit section links from the parser output. (1.24+)
    /// </summary>
    DisableEditSection = 4,
    /// <summary>
    /// Do not run HTML cleanup (e.g. tidy) on the parser output. (1.26+)
    /// </summary>
    DisableTidy = 8,
    /// <summary>
    /// Parse in preview mode. (1.22+)
    /// </summary>
    Preview = 0x1000,
    /// <summary>
    /// Parse in section preview mode (enables preview mode too). (1.22+)
    /// </summary>
    SectionPreview = 0x2000,
    /// <summary>
    /// Return parse output in a format suitable for mobile devices. (?)
    /// </summary>
    MobileFormat = 0x4000,
    /// <summary>
    /// Disable images in mobile output. (?)
    /// </summary>
    NoImages = 0x8000,
    /// <summary>
    /// Gives the structured limit report. (1.23+)
    /// This flag fills <see cref="ParsedContentInfo.ParserLimitReports"/>.
    /// </summary>
    LimitReport = 0x10000,
    /// <summary>
    /// Omit the limit report ("NewPP limit report") from the parser output. (1.17~1.22, <c>disablepp</c>; 1.23+, <c>disablelimitreport</c>)
    /// <see cref="ParsedContentInfo.ParserLimitReports"/> will be empty if both this flag and <see cref="LimitReport"/> is set.
    /// </summary>
    /// <remarks>By default, the limit report will be included as comment in the parsed HTML content.
    /// This flag can suppress such output.</remarks>
    DisableLimitReport = 0x20000,
    /// <summary>
    /// Includes language links supplied by extensions, in addition to the links specified on the page. (1.22+)
    /// </summary>
    EffectiveLanguageLinks = 0x40000,
    /// <summary>
    /// Gives the templates and other transcluded pages/modules in the parsed wikitext.
    /// </summary>
    TranscludedPages = 0x80000,
}