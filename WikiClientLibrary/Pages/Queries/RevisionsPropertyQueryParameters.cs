using System;
using System.Collections.Generic;
using System.Text;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Pages.Queries
{

    /// <summary>
    /// Returns GeoLocation of the page.
    /// <c>action=query&amp;prop=coordinates</c>
    /// (<a href="https://www.mediawiki.org/wiki/Extension:GeoData#prop.3Dcoordinates">mw:Extension:GeoData#prop=coordinates</a>)
    /// </summary>
    public class RevisionsPropertyQueryParameters : WikiPagePropertyQueryParameters
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
            return new KeyValuePairs<string, object>
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
    }
}
