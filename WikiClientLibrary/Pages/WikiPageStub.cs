using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using AsyncEnumerableExtensions;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Pages
{
    /// <summary>
    /// Contains basic information for identifying a page.
    /// </summary>
    /// <remarks>
    /// <para>Depending on the given information, the structure may contain page ID, page name, or both.
    /// If the owner does not know part of the information, these properties can be <c>0</c> and <c>null</c> respectively.</para>
    /// <para>This structure can also represent missing (or inexistent) pages. A missing page has either a page ID or
    /// a page name, not both. For the missing page with the given page ID, its <see cref="Title"/> should be
    /// <see cref="MissingPageTitle"/>; for the missing page with the given page title, the <see cref="Id"/>
    /// should be <ses cref="MissingPageId"/>. You can use <see cref="IsMissing"/> property to check for missing pages.</para>
    /// </remarks>
    public struct WikiPageStub : IEquatable<WikiPageStub>
    {

        /// <summary>The <see cref="Id"/> value used for missing page.</summary>
        /// <remarks>For how the missing pages are handled, see the "remarks" section of <see cref="WikiPage"/>.</remarks>
        public const int MissingPageId = -10000;

        /// <summary>The <see cref="Title"/> value used for missing page.</summary>
        /// <remarks>For how the missing pages are handled, see the "remarks" section of <see cref="WikiPage"/>.</remarks>
        public const string MissingPageTitle = "#Missing";

        /// <summary>The <see cref="NamespaceId"/> used when the constructor do not have namespace information about the page.</summary>
        public const int UnknownNamespaceId = -10000;

        /// <summary>
        /// An <see cref="WikiPageStub"/> that represents no page.
        /// This is the default value of the structure.
        /// </summary>
        public static readonly WikiPageStub Empty = new WikiPageStub();

        /// <inheritdoc cref="WikiPageStub(int,string,int)"/>
        public WikiPageStub(string title, int namespaceId) : this(0, title, namespaceId)
        {
        }

        /// <inheritdoc cref="WikiPageStub(int,string,int)"/>
        public WikiPageStub(int id) : this(id, null, UnknownNamespaceId)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="WikiPageStub"/>.
        /// </summary>
        /// <param name="id">Page ID.</param>
        /// <param name="title">Page full title.</param>
        /// <param name="namespaceId">Page namespace ID.</param>
        public WikiPageStub(int id, string title, int namespaceId) : this()
        {
            Id = id;
            Title = title;
            NamespaceId = namespaceId;
        }

        /// <summary>Gets the page ID.</summary>
        /// <value>Page ID; or <c>0</c> if the information is not avaiable;
        /// or <see cref="MissingPageId"/> for the confirmed missing page.</value>
        public int Id { get; }

        /// <summary>Gets the full title of the page.</summary>
        /// <value>Normalized or un-normalized page title; or <c>null</c> if the information is not avaiable;
        /// or <see cref="MissingPageTitle"/> for the confirmed missing page.</value>
        public string Title { get; }

        /// <summary>Gets the namespace ID of the page.</summary>
        /// <value>Namespace ID for the page; or <see cref="UnknownNamespaceId"/> if the information is not avaiable.</value>
        public int NamespaceId { get; }

        /// <summary>Checks whether the page is confirmed as missing.</summary>
        /// <remarks>If the returned value is <c>false</c>, the caller still cannot confirm the page
        /// does exist, unless otherwise informed so.</remarks>
        public bool IsMissing => Id == MissingPageId || Title == MissingPageTitle;

        /// <summary>
        /// Gets a value that indicates whether <see cref="Id"/> contains page ID information.
        /// </summary>
        public bool HasId => Id != 0 && Id != MissingPageId;

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
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is WikiPageStub && Equals((WikiPageStub)obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                if (IsEmpty) return 0;
                var hashCode = Id;
                hashCode = (hashCode * 397) ^ (Title != null ? Title.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ NamespaceId;
                return hashCode;
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            if (IsEmpty) return "<Empty>";
            if (Title == MissingPageTitle) return ("#Missing#" + Id);
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

        /// <summary>
        /// Construct a sequence of <see cref="WikiPageStub"/> from the given page IDs.
        /// </summary>
        /// <param name="site">The site in which to query for the pages.</param>
        /// <param name="ids">The page IDs to query.</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="site"/> or <paramref name="ids"/> is <c>null</c>.</exception>
        /// <returns>A sequence of <see cref="WikiPageStub"/> containing the page information.</returns>
        /// <remarks>For how the missing pages are handled, see the "remarks" section of <see cref="WikiPage"/>.</remarks>
        public static IAsyncEnumerable<WikiPageStub> FromPageIds(WikiSite site, IEnumerable<int> ids)
        {
            return AsyncEnumerableFactory.FromAsyncGenerator<WikiPageStub>(async (sink, ct) =>
            {
                var titleLimit = site.AccountInfo.HasRight(UserRights.ApiHighLimits)
                    ? 500
                    : 50;
                foreach (var partition in ids.Partition(titleLimit))
                {
                    var jresult = await site.GetJsonAsync(new MediaWikiFormRequestMessage(new
                    {
                        action = "query",
                        pageids = string.Join("|", partition),
                    }), ct);
                    Debug.Assert(jresult["query"] != null);
                    var jpages = jresult["query"]["pages"];
                    foreach (var id in partition)
                    {
                        var jpage = jpages[id.ToString(CultureInfo.InvariantCulture)];
                        if (jpage["missing"] == null)
                            sink.Yield(new WikiPageStub(id, (string)jpage["title"], (int)jpage["ns"]));
                        else
                            sink.Yield(new WikiPageStub(id, MissingPageTitle, UnknownNamespaceId));
                    }
                    await sink.Wait();
                }
            });
        }

        /// <summary>
        /// Construct a sequence of <see cref="WikiPageStub"/> from the given page titles.
        /// </summary>
        /// <param name="site">The site in which to query for the pages.</param>
        /// <param name="titles">The page IDs to query.</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="site"/> or <paramref name="titles"/> is <c>null</c>.</exception>
        /// <returns>A sequence of <see cref="WikiPageStub"/> containing the page information.</returns>
        /// <remarks>For how the missing pages are handled, see the "remarks" section of <see cref="WikiPage"/>.</remarks>
        public static IAsyncEnumerable<WikiPageStub> FromPageTitles(WikiSite site, IEnumerable<string> titles)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (titles == null) throw new ArgumentNullException(nameof(titles));
            return AsyncEnumerableFactory.FromAsyncGenerator<WikiPageStub>(async (sink, ct) =>
            {
                var titleLimit = site.AccountInfo.HasRight(UserRights.ApiHighLimits)
                    ? 500
                    : 50;
                foreach (var partition in titles.Partition(titleLimit))
                {
                    var jresult = await site.GetJsonAsync(new MediaWikiFormRequestMessage(new
                    {
                        action = "query",
                        titles = string.Join("|", partition),
                    }), ct);
                    Debug.Assert(jresult["query"] != null);
                    // Process title normalization.
                    var normalizedDict = jresult["query"]["normalized"]?.ToDictionary(n => (string)n["from"],
                        n => (string)n["to"]);
                    var pageDict = ((JObject)jresult["query"]["pages"]).Properties()
                        .ToDictionary(p => (string)p.Value["title"], p => p.Value);
                    foreach (var name in partition)
                    {
                        if (normalizedDict == null || !normalizedDict.TryGetValue(name, out var normalizedName))
                            normalizedName = name;
                        var jpage = pageDict[normalizedName];
                        if (jpage["missing"] == null)
                            sink.Yield(new WikiPageStub((int)jpage["pageid"], (string)jpage["title"], (int)jpage["ns"]));
                        else
                            sink.Yield(new WikiPageStub(MissingPageId, (string)jpage["title"], (int)jpage["ns"]));
                    }
                    await sink.Wait();
                }
            });
        }

    }
}
