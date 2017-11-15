using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Generators.Primitive;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// Searches for articles around the given point (determined either by coordinates or by article name).
    /// (<a href="https://www.mediawiki.org/wiki/Extension:GeoData#list%253Dgeosearch">mw:Extension:GeoData#list=geosearch</a>)
    /// </summary>
    public class GeoSearchGenerator : WikiPageGenerator<GeoSearchResultItem, WikiPage>
    {

        /// <inheritdoc />
        public GeoSearchGenerator(WikiSite site) : base(site)
        {
        }

        /// <summary>
        /// Gets/sets the coordinate around which to search.
        /// </summary>
        /// <remarks>The <see cref="GeoCoordinate.Dimension"/> property will be ignored.</remarks>
        public GeoCoordinate TargetCoordinate { get; set; }

        /// <summary>
        /// Gets/sets the title of page around which to search.
        /// </summary>
        /// <remarks>When this property is set to a non-null value, <see cref="TargetCoordinate"/> will be ignored.</remarks>
        public string TargetTitle { get; set; }

        /// <summary>
        /// Gets/sets the search radius in meters (10-10000).
        /// </summary>
        /// <remarks>The default value is <c>10</c>, i.e. 10 meters.</remarks>
        public double Radius { get; set; } = 10;

        /// <summary>
        /// Only list pages in these namespaces.
        /// </summary>
        /// <value>Selected ids of namespace, or <c>null</c> to use default settings (i.e. only in main namespace).</value>
        public IEnumerable<int> NamespaceIds { get; set; }

        /// <summary>
        /// Gets/set a value indicating whether to include secondary coordinates in the search results.
        /// </summary>
        public bool IncludesSecondaryCoordinates { get; set; }

        /// <inheritdoc />
        public override string ListName => "geosearch";

        /// <inheritdoc />
        public override IEnumerable<KeyValuePair<string, object>> EnumListParameters()
        {
            var prop = new Dictionary<string, object>
            {
                {"gsradius", Radius},
                {"gsnamespace", NamespaceIds == null ? null : string.Join("|", NamespaceIds)},
                {"gsprimary", IncludesSecondaryCoordinates ? "all" : "primary"},
                {"gsglobe", TargetCoordinate.Globe},
                {"gslimit", PaginationSize},
            };
            if (TargetTitle != null)
            {
                // When searching by page title, it would be better for MW API to
                // assume `gsglobe` corresponds to the `globe` of that page,
                // but there is currently no such behavior.
                prop["gspage"] = TargetTitle;
            }
            else
            {
                prop["gscoord"] = TargetCoordinate.Latitude + "|" + TargetCoordinate.Longitude;
            }
            return prop;
        }

        /// <inheritdoc />
        protected override GeoSearchResultItem ItemFromJson(JToken json)
        {
            return new GeoSearchResultItem(MediaWikiHelper.PageStubFromJson((JObject)json),
                MediaWikiHelper.GeoCoordinateFromJson((JObject)json),
                json["primary"] != null, (double)json["dist"]);
        }
    }
}
