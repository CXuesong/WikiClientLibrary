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
    public class FileInfoPropertyProvider : WikiPagePropertyProvider<CategoryInfoPropertyGroup>
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
        public override CategoryInfoPropertyGroup ParsePropertyGroup(JObject json)
        {
            return CategoryInfoPropertyGroup.Create(json);
        }

        /// <inheritdoc />
        public override string PropertyName => "imageinfo";
    }

    public class FileInfoPropertyGroup : WikiPagePropertyGroup
    {
        private static readonly FileInfoPropertyGroup Empty = new FileInfoPropertyGroup();

        private object _Revisions;

        public static FileInfoPropertyGroup Create(JObject jpage)
        {
            var jrevisions = jpage["imageinfo"];
            if (jrevisions == null) return null;
            if (!jrevisions.HasValues) return Empty;
            var stub = MediaWikiHelper.PageStubFromJson(jpage);
            return new FileInfoPropertyGroup(stub, (JArray)jrevisions);
        }

        private FileInfoPropertyGroup()
        {
            _Revisions = new FileRevision[0];
        }

        private FileInfoPropertyGroup(WikiPageStub page, JArray jrevisions)
        {
            if (jrevisions.Count == 1)
            {
                _Revisions = MediaWikiHelper.RevisionFromJson((JObject)jrevisions.First, page);
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
