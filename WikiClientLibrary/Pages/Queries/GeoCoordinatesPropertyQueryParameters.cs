using System;
using System.Collections.Generic;
using System.Text;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Pages.Queries
{
    /// <summary>
    /// Returns plain-text or limited HTML extracts of the given pages.
    /// <c>action=query&amp;prop=extracts</c>
    /// (<a href="https://www.mediawiki.org/wiki/Extension:TextExtracts#API">mw:Extension:TextExtracts#API</a>)
    /// </summary>
    public class GeoCoordinatesPropertyQueryParameters : WikiPagePropertyQueryParameters
    {

        public bool QueryPrimaryCoordinate { get; set; } = true;

        public bool QuerySecondaryCoordinate { get; set; }
        
        public GeoCoordinate QueryDistanceFromPoint { get; set; }

        public string QueryDistanceFromPage { get; set; }

        /// <inheritdoc />
        public override IEnumerable<KeyValuePair<string, object>> EnumParameters()
        {
            var p = new KeyValuePairs<string, object>
            {
                {"coprop", "globe|dim"},
            };
            if (QueryPrimaryCoordinate && QuerySecondaryCoordinate)
                p.Add("coprimary", "all");
            else if (QueryPrimaryCoordinate)
                p.Add("coprimary", "primary");
            else if (QuerySecondaryCoordinate)
                p.Add("coprimary", "secondary");
            else
                throw new ArgumentException("Either QueryPrimaryCoordinate or QuerySecondaryCoordinate should be true.");
            if (!QueryDistanceFromPoint.IsEmpty && QueryDistanceFromPage == null)
                throw new ArgumentException("Either QueryDistanceFromPoint or QueryDistanceFromPage should be non-set.");
            if (!QueryDistanceFromPoint.IsEmpty)
                p.Add("codistancefrompoint", QueryDistanceFromPoint.Latitude + "|" + QueryDistanceFromPoint.Longitude);
            if (QueryDistanceFromPage != null)
                p.Add("codistancefrompage", QueryDistanceFromPage);
            return p;
        }

        /// <inheritdoc />
        public override string PropertyName => "coordinates";
    }
}
