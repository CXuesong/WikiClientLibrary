﻿using System.Text.Json.Nodes;
using WikiClientLibrary.Generators.Primitive;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators;

/// <summary>
/// Gets random pages in a specific namespace.
/// (<a href="https://www.mediawiki.org/wiki/API:Random">mw:API:Random</a>, MediaWiki 1.12+)
/// </summary>
/// <remarks>
/// The max allowed <see cref="WikiList{TItem}.PaginationSize"/> is 10 for regular users,
/// and 20 for users with the <c>apihighlimits</c> right (typically in bot or sysop group).
/// </remarks>
/// <seealso cref="AllPagesGenerator"/>
public class RandomPageGenerator : WikiPageGenerator
{

    /// <inheritdoc />
    public RandomPageGenerator(WikiSite site) : base(site)
    {
    }

    /// <inheritdoc />
    protected override WikiPageStub ItemFromJson(JsonNode json)
    {
        // Note: page ID is contained in ["id"] rather than ["pageid"].
        return new WikiPageStub((long)json["id"], (string?)json["title"], (int)json["ns"]);
    }

    /// <summary>
    /// Only list pages in these namespaces.
    /// </summary>
    /// <value>Selected ids of namespace, or <c>null</c> if all the namespaces are selected.</value>
    public IEnumerable<int>? NamespaceIds { get; set; }

    /// <summary>
    /// How to filter redirects.
    /// </summary>
    public PropertyFilterOption RedirectsFilter { get; set; }

    /// <inheritdoc/>
    public override string ListName => "random";

    /// <inheritdoc/>
    public override IEnumerable<KeyValuePair<string, object?>> EnumListParameters()
    {
        var dict = new Dictionary<string, object?>
        {
            { "rnnamespace", NamespaceIds == null ? null : MediaWikiHelper.JoinValues(NamespaceIds) }, { "rnlimit", PaginationSize },
        };
        if (Site.SiteInfo.Version >= new MediaWikiVersion(1, 26))
        {
            dict.Add("rnfilterredir", RedirectsFilter.ToString("redirects", "nonredirects"));
        }
        else if (RedirectsFilter == PropertyFilterOption.WithProperty)
        {
            dict.Add("rnredirect", true);
            // for MW 1.26-, we cannot really implement RedirectsFilter == PropertyFilterOption.WithoutProperty
        }
        return dict;
    }

}
