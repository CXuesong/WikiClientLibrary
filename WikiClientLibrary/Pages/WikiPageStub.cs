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
    public struct WikiPageStub : IEquatable<WikiPageStub>
    {

        public const int MissingPageId = -10000;
        public const string MissingPageTitle = "#Missing";
        public const int UnknownNamespaceId = -10000;
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

        /// <summary>Page ID.</summary>
        public int Id { get; }

        /// <summary>Page full title.</summary>
        public string Title { get; }

        /// <summary>Page namespace ID.</summary>
        public int NamespaceId { get; }

        public bool IsMissing => Id == MissingPageId || Title == MissingPageTitle;

        public bool HasId => Id != 0 && Id != MissingPageId;

        public bool HasTitle => Title != null && Title != MissingPageTitle;

        public bool HasNamespaceId => NamespaceId != UnknownNamespaceId;

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

        public static bool operator ==(WikiPageStub left, WikiPageStub right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WikiPageStub left, WikiPageStub right)
        {
            return !left.Equals(right);
        }

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

        public static IAsyncEnumerable<WikiPageStub> FromPageTitles(WikiSite site, IEnumerable<string> titles)
        {
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
