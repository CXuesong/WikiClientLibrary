using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using WikiClientLibrary.Client;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Pages;

/// <summary>
/// Contains basic information for identifying a page.
/// </summary>
/// <remarks>
/// <para>Depending on the given information, the structure may contain page ID, page name, or both.
/// If the owner does not know part of the information, these properties can be <c>0</c> and <c>null</c> respectively.</para>
/// <para>This structure can also represent missing (or inexistent) pages. A missing page has either a page ID or
/// a page name, not both. For the missing page with the given page ID, its <see cref="Title"/> should be
/// <see cref="MissingPageTitle"/>; for the missing page with the given page title, the <see cref="Id"/>
/// should be <ses cref="MissingPageIdMask"/>. You can use <see cref="IsMissing"/> property to check for missing pages.</para>
/// <para>Since this structure can be implicitly converted from <see cref="string"/> (page title) and <see cref="int"/> (page ID),
/// you can use literal page title or page ID in the place where <see cref="WikiPageStub"/> is needed.</para>
/// </remarks>
public readonly struct WikiPageStub : IEquatable<WikiPageStub>
{

    /// <summary>The <see cref="Id"/> value used for missing page.</summary>
    /// <remarks>For how the missing pages are handled, see the "remarks" section of <see cref="WikiPageStub"/>.</remarks>
    public const long MissingPageIdMask = unchecked((long)0x8F00_0000_0000_0001);

    /// <summary>The <see cref="Id"/> value used for invalid page.</summary>
    /// <remarks>For how the missing pages are handled, see the "remarks" section of <see cref="WikiPageStub"/>.</remarks>
    public const long InvalidPageIdMask = unchecked((long)0x8F00_0000_0000_0002);

    /// <summary>The <see cref="Id"/> value used for Special page.</summary>
    public const long SpecialPageIdMask = unchecked((long)0x8F00_0000_0000_0011);

    /// <summary>The <see cref="Title"/> value used for missing page.</summary>
    /// <remarks>For how the missing pages are handled, see the "remarks" section of <see cref="WikiPageStub"/>.</remarks>
    public const string MissingPageTitle = "#Missing";

    /// <summary>The <see cref="NamespaceId"/> used when the constructor do not have namespace information about the page.</summary>
    public const int UnknownNamespaceId = -10000;

    /// <summary>
    /// An <see cref="WikiPageStub"/> that represents no page.
    /// This is the default value of the structure.
    /// </summary>
    public static readonly WikiPageStub Empty = new WikiPageStub();

    /// <inheritdoc cref="WikiPageStub(long,string,int)"/>
    public WikiPageStub(string title, int namespaceId) : this(0L, title, namespaceId)
    {
    }

    /// <inheritdoc cref="WikiPageStub(long,string,int)"/>
    public WikiPageStub(long id) : this(id, null, UnknownNamespaceId)
    {
    }

    /// <inheritdoc cref="WikiPageStub(long,string,int)"/>
    public WikiPageStub(string title) : this(0L, title, UnknownNamespaceId)
    {
    }

    [Obsolete("Use long as id type instead of int.")]
    public WikiPageStub(int id, string? title, int namespaceId) : this((long)id, title, namespaceId)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="WikiPageStub"/>.
    /// </summary>
    /// <param name="id">Page ID. <c>0</c> for unknown.</param>
    /// <param name="title">Page full title. <c>null</c> for unknown. <see cref="string.Empty"/> will be normalized into <c>null</c>.</param>
    /// <param name="namespaceId">Page namespace ID. <see cref="UnknownNamespaceId"/> for unknown.</param>
    public WikiPageStub(long id, string? title, int namespaceId)
    {
        const long idMasks = SpecialPageIdMask | MissingPageIdMask | InvalidPageIdMask;
        if (id < 0 && (id & idMasks) != id)
            throw new ArgumentOutOfRangeException(nameof(id),
                "Invalid page ID. ID should be positive; 0 for unknown; bitwise-or of one or more WikiPageStub.****Mask fields.");
        Id = id;
        Title = string.IsNullOrEmpty(title) ? null : title;
        NamespaceId = namespaceId;
    }

    public static WikiPageStub NewMissingPage(string title, int namespaceId)
    {
        return new WikiPageStub(MissingPageIdMask, title, namespaceId);
    }

    public static WikiPageStub NewMissingPage(long id)
    {
        return new WikiPageStub(id, MissingPageTitle, UnknownNamespaceId);
    }

    public static WikiPageStub NewSpecialPage(string title, int namespaceId)
    {
        return new WikiPageStub(SpecialPageIdMask, title, namespaceId);
    }

    public static WikiPageStub NewSpecialPage(string title, int namespaceId, bool isMissing)
    {
        if (isMissing)
            return new WikiPageStub(SpecialPageIdMask | MissingPageIdMask, title, namespaceId);
        return new WikiPageStub(SpecialPageIdMask, title, namespaceId);
    }

    public static WikiPageStub NewInvalidPage(string title)
    {
        return new WikiPageStub(InvalidPageIdMask, title, UnknownNamespaceId);
    }

    /// <summary>Gets the page ID.</summary>
    /// <value>Page ID; or <c>0</c> if the information is not available;
    /// or <see cref="MissingPageIdMask"/> for the confirmed missing page.</value>
    public long Id { get; }

    /// <summary>Gets the full title of the page.</summary>
    /// <value>Normalized or un-normalized page title; or <c>null</c> if the information is not available;
    /// or <see cref="MissingPageTitle"/> for the confirmed missing page.</value>
    public string? Title { get; }

    /// <summary>Gets the namespace ID of the page.</summary>
    /// <value>Namespace ID for the page; or <see cref="UnknownNamespaceId"/> if the information is not available.</value>
    public int NamespaceId { get; }

    /// <summary>Checks whether the page is confirmed as missing.</summary>
    /// <remarks>If the returned value is <c>false</c>, the caller still cannot confirm the page
    /// does exist, unless otherwise informed so.</remarks>
    public bool IsMissing => (Id & MissingPageIdMask) == MissingPageIdMask || Title == MissingPageTitle;

    /// <summary>Checks whether the structure represents a invalid page.</summary>
    /// <remarks>
    /// <para>An invalid page is usually a page reference with invalid title.</para>
    /// <para>If the returned value is <c>false</c>, the caller still cannot confirm the page
    /// does exist, unless otherwise informed so.</para>
    /// </remarks>
    public bool IsInvalid => (Id & InvalidPageIdMask) == InvalidPageIdMask;

    /// <summary>Checks whether the structure represents a Special page.</summary>
    /// <remarks>If the returned value is <c>false</c>, the caller still cannot confirm the page
    /// is not a Special page, unless otherwise informed so.</remarks>
    public bool IsSpecial => (Id & SpecialPageIdMask) == SpecialPageIdMask;

    /// <summary>
    /// Gets a value that indicates whether <see cref="Id"/> contains page ID information.
    /// </summary>
    public bool HasId => Id != 0 && Id != MissingPageIdMask && Id != SpecialPageIdMask;

    /// <summary>
    /// Gets a value that indicates whether <see cref="Title"/> contains page title information.
    /// </summary>
    public bool HasTitle => Title != null && Title != MissingPageTitle;

    /// <summary>
    /// Gets a value that indicates whether <see cref="Id"/> contains page namespace ID information.
    /// </summary>
    public bool HasNamespaceId => NamespaceId != UnknownNamespaceId;

    /// <summary>
    /// Checks whether the current structure does not represent an (existent or missing) page.
    /// </summary>
    public bool IsEmpty => !HasId && !HasTitle;

    /// <inheritdoc />
    public bool Equals(WikiPageStub other)
    {
        if (IsEmpty) return other.IsEmpty;
        else if (other.IsEmpty) return false;
        return Id == other.Id && string.Equals(Title, other.Title) && NamespaceId == other.NamespaceId;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        return obj is WikiPageStub other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Id, Title, NamespaceId);

    /// <inheritdoc />
    public override string ToString()
    {
        if (IsEmpty) return "<Empty>";
        if (Title == MissingPageTitle) return ("<Missing>#" + Id);
        return Title ?? ("#" + Id);
    }

    public static bool operator ==(WikiPageStub left, WikiPageStub right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(WikiPageStub left, WikiPageStub right)
    {
        return !left.Equals(right);
    }

    public static implicit operator WikiPageStub(string? pageTitle)
    {
        return string.IsNullOrEmpty(pageTitle) ? Empty : new WikiPageStub(pageTitle);
    }

    public static implicit operator WikiPageStub(long pageId)
    {
        return new WikiPageStub(pageId);
    }

    public static implicit operator WikiPageStub(long? pageId)
    {
        return new WikiPageStub(pageId ?? 0);
    }

    /// <summary>
    /// Construct a sequence of <see cref="WikiPageStub"/> from the given page IDs.
    /// </summary>
    /// <param name="site">The site in which to query for the pages.</param>
    /// <param name="ids">The page IDs to query.</param>
    /// <exception cref="ArgumentNullException">Either <paramref name="site"/> or <paramref name="ids"/> is <c>null</c>.</exception>
    /// <returns>A sequence of <see cref="WikiPageStub"/> containing the page information.</returns>
    /// <remarks>For how the missing pages are handled, see the "remarks" section of <see cref="WikiPage"/>.</remarks>
    public static async IAsyncEnumerable<WikiPageStub> FromPageIds(WikiSite site, IEnumerable<long> ids,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var titleLimit = site.AccountInfo.HasRight(UserRights.ApiHighLimits)
            ? 500
            : 50;
        foreach (var partition in ids.Partition(titleLimit))
        {
            var jresult = await site.InvokeMediaWikiApiAsync(
                new MediaWikiFormRequestMessage(new { action = "query", pageids = MediaWikiHelper.JoinValues(partition), }),
                cancellationToken);
            Debug.Assert(jresult["query"] != null);
            var jpages = jresult["query"]["pages"];
            using (ExecutionContextStash.Capture())
                foreach (var id in partition)
                {
                    var jpage = jpages[id.ToString(CultureInfo.InvariantCulture)];
                    if (jpage["missing"] == null)
                        yield return new WikiPageStub(id, (string?)jpage["title"], (int)jpage["ns"]);
                    else
                        yield return new WikiPageStub(id, MissingPageTitle, UnknownNamespaceId);
                }
        }
    }

    /// <summary>
    /// Construct a sequence of <see cref="WikiPageStub"/> from the given page titles.
    /// </summary>
    /// <param name="site">The site in which to query for the pages.</param>
    /// <param name="titles">The page IDs to query.</param>
    /// <exception cref="ArgumentNullException">Either <paramref name="site"/> or <paramref name="titles"/> is <c>null</c>.</exception>
    /// <returns>A sequence of <see cref="WikiPageStub"/> containing the page information.</returns>
    /// <remarks>For how the missing pages are handled, see the "remarks" section of <see cref="WikiPage"/>.</remarks>
    public static async IAsyncEnumerable<WikiPageStub> FromPageTitles(WikiSite site, IEnumerable<string> titles,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (site == null) throw new ArgumentNullException(nameof(site));
        if (titles == null) throw new ArgumentNullException(nameof(titles));
        var titleLimit = site.AccountInfo.HasRight(UserRights.ApiHighLimits)
            ? 500
            : 50;
        foreach (var partition in titles.Partition(titleLimit))
        {
            var jresult = await site.InvokeMediaWikiApiAsync(
                new MediaWikiFormRequestMessage(new
                {
                    action = "query", titles = MediaWikiHelper.JoinValues(partition),
                }),
                cancellationToken);
            Debug.Assert(jresult["query"] != null);
            // Process title normalization.
            var normalizedDict = jresult["query"]["normalized"]?.AsArray().ToDictionary(
                n => (string)n["from"]!,
                n => (string)n["to"]
            );
            var pageDict = jresult["query"]["pages"].AsObject()
                .ToDictionary(p => (string)p.Value["title"]!, p => p.Value);
            using var _ = ExecutionContextStash.Capture();
            foreach (var name in partition)
            {
                if (normalizedDict == null || !normalizedDict.TryGetValue(name, out var normalizedName))
                    normalizedName = name;
                var jpage = pageDict[normalizedName];
                if (jpage["missing"] == null)
                    yield return new WikiPageStub((long)jpage["pageid"], (string?)jpage["title"], (int)jpage["ns"]);
                else
                    yield return new WikiPageStub(MissingPageIdMask, (string?)jpage["title"], (int)jpage["ns"]);
            }
        }
    }

}
