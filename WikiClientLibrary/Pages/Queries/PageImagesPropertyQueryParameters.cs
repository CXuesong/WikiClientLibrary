using System;
using System.Collections.Generic;
using System.Text;

namespace WikiClientLibrary.Pages.Queries
{

    /// <summary>
    /// Returns information about images on the page, such as thumbnail and presence of photos.
    /// <c>action=query&amp;prop=pageimages</c>
    /// (<a href="https://www.mediawiki.org/wiki/Special:MyLanguage/Extension:PageImages#API">mw:Extension:PageImages#API</a>)
    /// </summary>
    public class PageImagesPropertyQueryParameters : WikiPagePropertyQueryParameters
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
        public override IEnumerable<KeyValuePair<string, object>> EnumParameters()
        {
            var p  = new KeyValuePairs<string, object>
            {
                { "pilimit", "50"},
            };
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
    }
}
