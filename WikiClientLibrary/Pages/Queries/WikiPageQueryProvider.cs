﻿using System.Text;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages.Queries.Properties;

namespace WikiClientLibrary.Pages.Queries;

/// <summary>
/// Provides basic MediaWiki API request parameters for <c>action=query&amp;titles=</c>
/// or <c>action=query&amp;pageids=</c> requests.
/// </summary>
/// <remarks>The default implementation of this interface is <see cref="WikiPageQueryProvider"/>.</remarks>
public interface IWikiPageQueryProvider
{
    /// <summary>
    /// Enumerates the MediaWiki API request parameters for <c>action=query</c> request.
    /// </summary>
    /// <param name="version"></param>
    IEnumerable<KeyValuePair<string, object?>> EnumParameters(MediaWikiVersion version);

    /// <summary>
    /// Gets the maximum allowed count of titles in each MediaWiki API request.
    /// </summary>
    /// <param name="version">MediaWiki API version. Use <seealso cref="MediaWikiVersion.Zero"/> for unknown version or compatible mode.</param>
    /// <param name="apiHighLimits">Whether the account has <c>api-highlimits</c> right.</param>
    /// <returns>
    /// The maximum allowed count of titles in each MediaWiki API request.
    /// This applies to the values of <c>ids=</c> and <c>titles=</c> parameters
    /// for <c>action=query</c> request.
    /// </returns>
    int GetMaxPaginationSize(MediaWikiVersion version, bool apiHighLimits);

    /// <summary>
    /// Parses one or more property groups from the given<c>action=query</c> JSON response.
    /// </summary>
    /// <param name="json">One of the item node under the JSON path <c>query/pages</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is <c>null</c>.</exception>
    /// <returns>A sequence of property group instance, or <see cref="Enumerable.Empty{IWikiPagePropertyGroup}"/> if no property groups available.</returns>
    IEnumerable<IWikiPagePropertyGroup> ParsePropertyGroups(JObject json);
}

/// <summary>
/// The default implementation of <see cref="IWikiPageQueryProvider"/> that generates parameters for
/// <c>action=query&amp;titles=</c> or <c>action=query&amp;pageids=</c> MediaWiki API requests
/// with a set of <see cref="IWikiPagePropertyProvider{T}"/>.
/// </summary>
/// <inheritdoc />
public class WikiPageQueryProvider : IWikiPageQueryProvider
{

    private ICollection<IWikiPagePropertyProvider<IWikiPagePropertyGroup>>? _Properties;

    /// <summary>
    /// Initializes a <see cref="WikiPageQueryProvider"/> from the given <see cref="PageQueryOptions"/> value.
    /// </summary>
    /// <param name="options">The page query options.</param>
    /// <returns>The equivalent <see cref="WikiPageQueryProvider"/> that can be further modified by the caller.</returns>
    /// <remarks>If you won't perform any customizations on the returned instance,
    /// consider using <see cref="MediaWikiHelper.QueryProviderFromOptions"/>.</remarks>
    public static WikiPageQueryProvider FromOptions(PageQueryOptions options)
    {
            if ((options & (PageQueryOptions.FetchContent | PageQueryOptions.ResolveRedirects)) != options)
                throw new ArgumentException(Prompts.ExceptionInvalidEnumValue, nameof(options));
            var provider = new WikiPageQueryProvider
            {
                Properties = new List<IWikiPagePropertyProvider<IWikiPagePropertyGroup>>
                {
                    new PageInfoPropertyProvider { },
                    new RevisionsPropertyProvider {FetchContent = (options & PageQueryOptions.FetchContent) == PageQueryOptions.FetchContent},
                    new CategoryInfoPropertyProvider { },
                    new PagePropertiesPropertyProvider { },
                    new FileInfoPropertyProvider { },
                },
                ResolveRedirects = (options & PageQueryOptions.ResolveRedirects) == PageQueryOptions.ResolveRedirects
            };
            return provider;
        }

    /// <summary>
    /// Resolves directs automatically. This may later change <see cref="WikiPage.Title"/>.
    /// This option cannot be used with generators.
    /// In the case of multiple redirects (A→B→C→…→X), all the redirects on the path will be resolved.
    /// </summary>
    public bool ResolveRedirects { get; set; }

    /// <summary>
    /// Gets/sets the page properties to fetch from MediaWiki site.
    /// </summary>
    public ICollection<IWikiPagePropertyProvider<IWikiPagePropertyGroup>> Properties
    {
        get
        {
                if (_Properties == null) _Properties = new List<IWikiPagePropertyProvider<IWikiPagePropertyGroup>>();
                return _Properties;
            }
        set { _Properties = value; }
    }

    /// <param name="version"></param>
    /// <inheritdoc />
    public virtual IEnumerable<KeyValuePair<string, object?>> EnumParameters(MediaWikiVersion version)
    {
            var propBuilder = new StringBuilder();
            var p = new OrderedKeyValuePairs<string, object?>
            {
                {"action", "query"},
                {"redirects", ResolveRedirects},
                {"maxlag", 5},
            };
            if (_Properties != null)
            {
                foreach (var prop in _Properties)
                {
                    if (prop.PropertyName != null)
                    {
                        if (propBuilder.Length > 0) propBuilder.Append('|');
                        propBuilder.Append(prop.PropertyName);
                    }
                    p.AddRange(prop.EnumParameters(version));
                }
            }
            p.Add("prop", propBuilder.ToString());
            return p;
        }

    /// <inheritdoc />
    public virtual int GetMaxPaginationSize(MediaWikiVersion version, bool apiHighLimits)
    {
            var limit = apiHighLimits ? 500 : 5000;
            if (_Properties != null)
            {
                foreach (var prop in _Properties)
                {
                    limit = Math.Min(limit, prop.GetMaxPaginationSize(version, apiHighLimits));
                }
            }
            return limit;
        }

    /// <inheritdoc />
    public virtual IEnumerable<IWikiPagePropertyGroup> ParsePropertyGroups(JObject json)
    {
            if (_Properties == null) yield break;
            foreach (var provider in _Properties)
            {
                var group = provider.ParsePropertyGroup(json);
                if (group != null) yield return group;
            }
        }
}

internal class SealedWikiPageQueryProvider : IWikiPageQueryProvider
{
    private readonly IWikiPageQueryProvider underlyingProvider;

    public SealedWikiPageQueryProvider(IWikiPageQueryProvider underlyingProvider)
    {
            this.underlyingProvider = underlyingProvider ?? throw new ArgumentNullException(nameof(underlyingProvider));
        }

    /// <param name="version"></param>
    /// <inheritdoc />
    public IEnumerable<KeyValuePair<string, object?>> EnumParameters(MediaWikiVersion version)
    {
            return underlyingProvider.EnumParameters(version);
        }

    /// <inheritdoc />
    public int GetMaxPaginationSize(MediaWikiVersion version, bool apiHighLimits)
    {
            return underlyingProvider.GetMaxPaginationSize(version, apiHighLimits);
        }

    /// <inheritdoc />
    public IEnumerable<IWikiPagePropertyGroup> ParsePropertyGroups(JObject json)
    {
            return underlyingProvider.ParsePropertyGroups(json);
        }
}