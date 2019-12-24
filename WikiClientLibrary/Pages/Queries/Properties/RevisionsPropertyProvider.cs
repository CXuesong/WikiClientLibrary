using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Pages.Queries.Properties
{

    /// <summary>
    /// Returns the latest revision of the page.
    /// (<a href="https://www.mediawiki.org/wiki/API:Revisions">mw:API:Revisions</a>, MediaWiki 1.8+)
    /// </summary>
    /// <remarks>
    /// The <c>prop=revisions</c> module has been implemented as
    /// <see cref="RevisionsPropertyProvider"/> and <see cref="RevisionsGenerator"/>.
    /// The former allows you to fetch for the latest revisions for multiple pages,
    /// while the latter allows you to enumerate the revisions of a single page.
    /// </remarks>
    public class RevisionsPropertyProvider : WikiPagePropertyProvider<RevisionsPropertyGroup>
    {

        /// <summary>
        /// Gets/sets a value that determines whether to fetch revision content.
        /// If set, the maximum limit per API request will be 10 times as low.
        /// (Note: If you want HTML rather than wikitext, use action=parse instead.)
        /// </summary>
        public bool FetchContent { get; set; }

        /// <summary>
        /// Gets/sets the names of the revision slot from which to retrieve the revisions. (MediaWiki 1.32+)
        /// </summary>
        /// <value>
        /// A sequence of slot names, or <c>null</c> to use default slot names (<c>["main"]</c>).
        /// </value>
        /// <remarks>
        /// <para>See <see cref="RevisionSlot"/> for more information on "slot"s.</para>
        /// </remarks>
        public IEnumerable<string> Slots { get; set; }

        internal static readonly object RVLIMIT_SINGLE_REVISION_MAGIC = "[single]";

        /// <inheritdoc />
        public override IEnumerable<KeyValuePair<string, object>> EnumParameters(MediaWikiVersion version)
        {
            var p = new OrderedKeyValuePairs<string, object>
            {
                {
                    "rvprop", FetchContent
                        ? "ids|timestamp|flags|comment|user|userid|contentmodel|sha1|tags|size|content"
                        : "ids|timestamp|flags|comment|user|userid|contentmodel|sha1|tags|size"
                },
                // This field should be post-processed in RequestHelper.RefreshPagesAsync
                { "rvlimit", RVLIMIT_SINGLE_REVISION_MAGIC }
            };
            if (Slots != null || version >= new MediaWikiVersion(1, 32))
            {
                // If user specified Slots explicitly, then we will respect it regardless of MW version.
                p.Add("rvslots", Slots == null ? RevisionSlot.MainSlotName : MediaWikiHelper.JoinValues(Slots));
            }
            return p;
        }

        /// <inheritdoc />
        public override int GetMaxPaginationSize(MediaWikiVersion version, bool apiHighLimits) => base.GetMaxPaginationSize(version, apiHighLimits) / 10;

        /// <inheritdoc />
        public override string PropertyName => "revisions";

        /// <inheritdoc />
        public override RevisionsPropertyGroup ParsePropertyGroup(JObject json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            return RevisionsPropertyGroup.Create(json);
        }

    }

    public class RevisionsPropertyGroup : WikiPagePropertyGroup
    {
        private static readonly RevisionsPropertyGroup Empty = new RevisionsPropertyGroup();

        private object _Revisions;

        internal static RevisionsPropertyGroup Create(JObject jpage)
        {
            var jrevisions = jpage["revisions"];
            if (jrevisions == null) return null;
            if (!jrevisions.HasValues) return Empty;
            var stub = MediaWikiHelper.PageStubFromJson(jpage);
            return new RevisionsPropertyGroup(stub, (JArray)jrevisions);
        }

        private RevisionsPropertyGroup()
        {
            _Revisions = new Revision[0];
        }

        private RevisionsPropertyGroup(WikiPageStub page, JArray jrevisions)
        {
            if (jrevisions.Count == 1)
            {
                _Revisions = MediaWikiHelper.RevisionFromJson((JObject)jrevisions.First, page);
            }
            else
            {
                _Revisions = new ReadOnlyCollection<Revision>(jrevisions
                    .Select(jr => MediaWikiHelper.RevisionFromJson((JObject)jr, page))
                    .ToArray());
            }
        }

        public IReadOnlyCollection<Revision> Revisions
        {
            get
            {
                if (_Revisions is Revision rev)
                    _Revisions = new ReadOnlyCollection<Revision>(new[] { rev });
                return (IReadOnlyCollection<Revision>)_Revisions;
            }
        }

        public Revision LatestRevision
        {
            get
            {
                var localRev = _Revisions;
                if (localRev is Revision rev) return rev;
                var revs = (IReadOnlyList<Revision>)localRev;
                if (revs.Count == 0) return null;
                if (revs[0].TimeStamp >= revs[revs.Count - 1].TimeStamp)
                    return revs[0];
                return revs[revs.Count - 1];
            }
        }

    }
}
