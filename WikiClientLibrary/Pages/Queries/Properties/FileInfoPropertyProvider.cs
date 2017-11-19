using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Pages.Queries.Properties
{

    /// <summary>
    /// Provides information for MediaWiki files.
    /// (<a href="https://www.mediawiki.org/wiki/API:Fileinfo">mw:API:Fileinfo</a>, MediaWiki 1.13+)
    /// </summary>
    public class FileInfoPropertyProvider : WikiPagePropertyProvider<FileInfoPropertyGroup>
    {
        /// <inheritdoc />
        public override IEnumerable<KeyValuePair<string, object>> EnumParameters()
        {
            return new OrderedKeyValuePairs<string, object>
            {
                {"iiprop", "timestamp|user|comment|url|size|sha1"},
            };
        }

        /// <inheritdoc />
        public override FileInfoPropertyGroup ParsePropertyGroup(JObject json)
        {
            return FileInfoPropertyGroup.Create(json);
        }

        /// <inheritdoc />
        public override string PropertyName => "imageinfo";
    }

    /// <summary>
    /// Contains properties for MediaWiki files.
    /// (<a href="https://www.mediawiki.org/wiki/API:Fileinfo">mw:API:Fileinfo</a>, MediaWiki 1.13+)
    /// </summary>
    public class FileInfoPropertyGroup : WikiPagePropertyGroup
    {
        private static readonly FileInfoPropertyGroup Empty = new FileInfoPropertyGroup();

        private object _Revisions;

        internal static FileInfoPropertyGroup Create(JObject jpage)
        {
            var info = jpage["imageinfo"];
            // jpage["imageinfo"] == null indicates the page may not be a valid File.
            if (info == null) return null;
            if (!info.HasValues) return Empty;
            var stub = MediaWikiHelper.PageStubFromJson(jpage);
            return new FileInfoPropertyGroup(stub, (JArray)info);
        }

        private FileInfoPropertyGroup()
        {
            _Revisions = new FileRevision[0];
        }

        private FileInfoPropertyGroup(WikiPageStub page, JArray jrevisions)
        {
            if (jrevisions.Count == 1)
            {
                _Revisions = MediaWikiHelper.FileRevisionFromJson((JObject)jrevisions.First, page);
            }
            else
            {
                _Revisions = new ReadOnlyCollection<FileRevision>(jrevisions
                    .Select(jr => MediaWikiHelper.FileRevisionFromJson((JObject)jr, page))
                    .ToArray());
            }
        }

        public IReadOnlyCollection<FileRevision> Revisions
        {
            get
            {
                if (_Revisions is FileRevision rev)
                    _Revisions = new ReadOnlyCollection<FileRevision>(new[] { rev });
                return (IReadOnlyCollection<FileRevision>)_Revisions;
            }
        }

        /// <summary>
        /// Gets the latest file revision information.
        /// </summary>
        public FileRevision LatestRevision
        {
            get
            {
                var localRev = _Revisions;
                if (localRev is FileRevision rev) return rev;
                var revs = (IReadOnlyList<FileRevision>)localRev;
                if (revs.Count == 0) return null;
                if (revs[0].TimeStamp >= revs[revs.Count - 1].TimeStamp)
                    return revs[0];
                return revs[revs.Count - 1];
            }
        }

    }

}
