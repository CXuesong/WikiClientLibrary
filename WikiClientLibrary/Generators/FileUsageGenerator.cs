using WikiClientLibrary.Generators.Primitive;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators;

/// <summary>
/// Generates all the pages that transclude the specified file.
/// </summary>
/// <seealso cref="BacklinksGenerator"/>
/// <seealso cref="TranscludedInGenerator"/>
/// <seealso cref="TransclusionsGenerator"/>
public class FileUsageGenerator : WikiPageGenerator
{

    /// <inheritdoc />
    public FileUsageGenerator(WikiSite site) : base(site)
    {
    }

    /// <inheritdoc />
    /// <param name="targetTitle">List pages transclude this file. The file does not need to exist.</param>
    public FileUsageGenerator(WikiSite site, string targetTitle) : base(site)
    {
        TargetTitle = targetTitle;
    }

    /// <summary>
    /// List pages transcluding this file. The file does not need to exist.
    /// </summary>
    public string TargetTitle { get; set; } = "";

    /// <summary>
    /// Only list pages in these namespaces.
    /// </summary>
    /// <value>Selected ids of namespace, or <c>null</c> if all the namespaces are selected.</value>
    public IEnumerable<int>? NamespaceIds { get; set; }

    /// <summary>
    /// How to filter redirects in the results.
    /// </summary>
    public PropertyFilterOption RedirectsFilter { get; set; }

    /// <inheritdoc />
    public override string ListName => "imageusage";

    /// <inheritdoc />
    public override IEnumerable<KeyValuePair<string, object?>> EnumListParameters()
    {
        return new Dictionary<string, object?>
        {
            {"iutitle", TargetTitle},
            {"iunamespace", NamespaceIds == null ? null : MediaWikiHelper.JoinValues(NamespaceIds)},
            {"iufilterredir", RedirectsFilter.ToString("redirects", "nonredirects")},
            {"iulimit", PaginationSize}
        };
    }
}