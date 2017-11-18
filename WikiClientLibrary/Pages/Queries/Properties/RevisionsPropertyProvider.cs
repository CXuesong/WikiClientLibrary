using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Pages.Queries.Properties
{

    /// <summary>
    /// Returns GeoLocation of the page.
    /// <c>action=query&amp;prop=coordinates</c>
    /// (<a href="https://www.mediawiki.org/wiki/Extension:GeoData#prop.3Dcoordinates">mw:Extension:GeoData#prop=coordinates</a>)
    /// </summary>
    public class RevisionsPropertyProvider : WikiPagePropertyProvider<RevisionsPropertyGroup>
    {

        /// <summary>
        /// Gets/sets a value that determines whether to fetch revision content.
        /// If set, the maximum limit per API request will be 10 times as low.
        /// (Note: If you want HTML rather than wikitext, use action=parse instead.)
        /// </summary>
        public bool FetchContent { get; set; }

        /// <inheritdoc />
        public override IEnumerable<KeyValuePair<string, object>> EnumParameters()
        {
            return new OrderedKeyValuePairs<string, object>
            {
                {
                    "rvprop",
                    FetchContent
                        ? "ids|timestamp|flags|comment|user|userid|contentmodel|sha1|tags|size|content"
                        : "ids|timestamp|flags|comment|user|userid|contentmodel|sha1|tags|size"
                },
            };
        }

        /// <inheritdoc />
        public override int GetMaxPaginationSize(bool apiHighLimits) => base.GetMaxPaginationSize(apiHighLimits) / 10;

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
