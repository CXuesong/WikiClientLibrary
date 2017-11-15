using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace WikiClientLibrary.Pages.Queries.Properties
{

    /// <summary>
    /// Returns information about images on the page, such as thumbnail and presence of photos.
    /// <c>action=query&amp;prop=pageimages</c>
    /// (<a href="https://www.mediawiki.org/wiki/Special:MyLanguage/Extension:PageImages#API">mw:Extension:PageImages#API</a>)
    /// </summary>
    public class PageImagePropertyProvider : WikiPagePropertyProvider
    {

        /// <summary>
        /// Gets/sets a value that determines whether to fetch for URL and dimensions of thumbnail image associated with page, if any.
        /// </summary>
        public bool QueryOriginalImage { get; set; }

        /// <summary>
        /// Gets/sets the maximum thumbnail dimension.
        /// </summary>
        /// <value>Maximum thumbnail dimension, in px; or <c>0</c> to disable thumbnail image fetching.</value>
        /// <remarks>The default value is 50.</remarks>
        public int ThumbnailSize { get; set; } = 50;

        /// <inheritdoc />
        public override int GetMaxPaginationSize(bool apiHighLimits)
        {
            return apiHighLimits ? 100 : 50;
        }

        /// <inheritdoc />
        public override IEnumerable<KeyValuePair<string, object>> EnumParameters()
        {
            var p = new KeyValuePairs<string, object>();
            if (QueryOriginalImage && ThumbnailSize > 0)
                p.Add("piprop", "original|thumbnail|name");
            else if (QueryOriginalImage)
                p.Add("piprop", "original|name");
            else if (ThumbnailSize > 0)
                p.Add("piprop", "thumbnail|name");
            else
                p.Add("piprop", "name");
            if (ThumbnailSize >= 0)
                p.Add("pithumbsize", ThumbnailSize);
            return p;
        }

        /// <inheritdoc />
        public override string PropertyName => "pageimages";

        /// <inheritdoc />
        public override IWikiPagePropertyGroup ParsePropertyGroup(JObject json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            return PageImagePropertyGroup.Create(json);
        }

    }

    public struct PageImageInfo : IEquatable<PageImageInfo>
    {

        public static readonly PageImageInfo Empty = new PageImageInfo();

        public PageImageInfo(string url, int width, int height) : this()
        {
            Url = url;
            Width = width;
            Height = height;
        }

        public string Url { get; }

        public int Width { get; }

        public int Height { get; }

        /// <inheritdoc />
        public bool Equals(PageImageInfo other)
        {
            return string.Equals(Url, other.Url) && Width == other.Width && Height == other.Height;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is PageImageInfo && Equals((PageImageInfo)obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Url != null ? Url.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ Width;
                hashCode = (hashCode * 397) ^ Height;
                return hashCode;
            }
        }

        public static bool operator ==(PageImageInfo left, PageImageInfo right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PageImageInfo left, PageImageInfo right)
        {
            return !left.Equals(right);
        }
    }

    public class PageImagePropertyGroup : WikiPagePropertyGroup
    {

        private static readonly PageImagePropertyGroup Empty = new PageImagePropertyGroup();

        internal static PageImagePropertyGroup Create(JToken jpage)
        {
            if (jpage["original"] == null && jpage["thumbnail"] == null && jpage["pageimage"] == null)
                return Empty;
            return new PageImagePropertyGroup(jpage);
        }

        private PageImagePropertyGroup()
        {
            OriginalImage = PageImageInfo.Empty;
            ThumbnailImage = PageImageInfo.Empty;
            ImageTitle = null;
        }

        private PageImagePropertyGroup(JToken jpage)
        {
            OriginalImage = jpage["original"] != null ? ParseImageInfo(jpage["original"]) : PageImageInfo.Empty;
            ThumbnailImage = jpage["thumbnail"] != null ? ParseImageInfo(jpage["thumbnail"]) : PageImageInfo.Empty;
            ImageTitle = (string)jpage["pageimage"];
        }

        private static PageImageInfo ParseImageInfo(JToken root)
        {
            return new PageImageInfo((string)root["source"], (int)root["width"], (int)root["height"]);
        }

        public PageImageInfo OriginalImage { get; }

        public PageImageInfo ThumbnailImage { get; }

        public string ImageTitle { get; }


    }
}
