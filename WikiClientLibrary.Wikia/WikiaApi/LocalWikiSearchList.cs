using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Generators.Primitive;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;
using WikiClientLibrary.Wikia.Sites;

namespace WikiClientLibrary.Wikia.WikiaApi;

/// <summary>
/// A MediaWiki-<c>list</c>-like object that can enumerate the search results.
/// </summary>
/// <remarks>To take a sequence of <see cref="WikiPage"/> instances from this object,
/// you can use LINQ <see cref="Enumerable.Select{TSource,TResult}(IEnumerable{TSource},Func{TSource,TResult})"/> method
/// with <see cref="WikiPage(WikiSite,string)"/>; then call <see cref="Pages.WikiPageExtensions.RefreshAsync(IEnumerable{WikiPage})"/>
/// to fetch page information or content.</remarks>
/// <seealso cref="SearchGenerator"/>
public class LocalWikiSearchList : IWikiList<LocalWikiSearchResultItem>
{

    private int _PaginationSize = 25;
    private int _MinimumArticleQuality = 10;

    private static readonly IList<int> defaultNamespace = new ReadOnlyCollection<int>(new[]
    {
        BuiltInNamespaces.Main, BuiltInNamespaces.Category,
    });

    /// <summary>
    /// Initializes a new instance of <see cref="LocalWikiSearchList"/> using the target
    /// Wikia site.
    /// </summary>
    /// <inheritdoc cref="LocalWikiSearchList(WikiaSite,string)"/>
    public LocalWikiSearchList(WikiaSite site) : this(site, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="LocalWikiSearchList"/> using the target
    /// Wikia site and search keyword.
    /// </summary>
    /// <param name="site">The Wikia site this list-like object is applied to.</param>
    /// <param name="keyword">Search for all page titles (or content) that have this value.</param>
    public LocalWikiSearchList(WikiaSite site, string keyword)
    {
        Site = site ?? throw new ArgumentNullException(nameof(site));
        Keyword = keyword;
    }

    /// <summary>
    /// Gets the Wikia site this list-like object is applied to.
    /// </summary>
    public WikiaSite Site { get; }

    /// <summary>
    /// Search for all page titles (or content) that have this value.
    /// </summary>
    public string Keyword { get; set; }

    /// <summary>
    /// Only list pages in these namespaces.
    /// </summary>
    /// <value>The namespace(s) to enumerate. No more than 50 (500 for bots) allowed.
    /// See <see cref="BuiltInNamespaces"/> for a list of MediaWiki built-in namespace IDs.
    /// Set to <c>null</c> to use default namespace selection.
    /// (Default: [0, 14], i.e. <see cref="BuiltInNamespaces.Main"/> &amp; <see cref="BuiltInNamespaces.Category"/>)</value>
    public IEnumerable<int> NamespaceIds { get; set; } = defaultNamespace;

    /// <summary>
    /// The ranking to use in fetching the list of results.
    /// </summary>
    public SearchRankingType RankingType { get; set; }

    /// <summary>
    /// Minimal value of article quality. 
    /// </summary>
    /// <value>Ranges from 0 to 99.</value>
    public int MinimumArticleQuality
    {
        get { return _MinimumArticleQuality; }
        set
        {
            if (value < 0 || value > 99)
                throw new ArgumentOutOfRangeException(nameof(value), "Article quality should be in the range of 0~99.");
            _MinimumArticleQuality = value;
        }
    }

    /// <summary>
    /// Gets/sets maximum items returned per API invocation.
    /// </summary>
    /// <value>
    /// Maximum count of items returned per MediaWiki API invocation.
    /// This limit is 25 by default, and can be set as high as 200 for regular users.
    /// </value>
    /// <remarks>
    /// This property decides how many items returned at most per API invocation.
    /// </remarks>
    public int PaginationSize
    {
        get { return _PaginationSize; }
        set
        {
            if (value < 1) throw new ArgumentOutOfRangeException(nameof(value));
            _PaginationSize = value;
        }
    }

    private string SerializeRank(SearchRankingType value)
    {
        return value switch
        {
            SearchRankingType.Default => "default",
            SearchRankingType.Newest => "newest",
            SearchRankingType.Oldest => "oldest",
            SearchRankingType.RecentlyModified => "recently-modified",
            SearchRankingType.Stable => "Stable",
            SearchRankingType.MostViewed => "most-viewed",
            SearchRankingType.Freshest => "freshest",
            SearchRankingType.Stalest => "stalest",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
        };
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<LocalWikiSearchResultItem> EnumItemsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int totalBatches = 1;
        for (int currentBatch = 1; currentBatch <= totalBatches; currentBatch++)
        {
            var jresult = await Site.InvokeWikiaApiAsync("/Search/List", new WikiaQueryRequestMessage(new
            {
                query = Keyword,
                type = "articles",
                rank = SerializeRank(RankingType),
                limit = PaginationSize,
                minArticleQuality = MinimumArticleQuality,
                namespaces = NamespaceIds == null ? null : string.Join(",", NamespaceIds),
                batch = currentBatch,
            }), cancellationToken);
            totalBatches = (int)jresult["batches"];
            using (ExecutionContextStash.Capture())
                foreach (var i in jresult["items"].AsArray())
                    yield return i.Deserialize<LocalWikiSearchResultItem>(Utility.WikiaApiJsonSerializerOptions);
        }
    }

}

public enum SearchRankingType
{

    Default,
    Newest,
    Oldest,
    RecentlyModified,
    Stable,
    MostViewed,
    Freshest,
    Stalest,

}
