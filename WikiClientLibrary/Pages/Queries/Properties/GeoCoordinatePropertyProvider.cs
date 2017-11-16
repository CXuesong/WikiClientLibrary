using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Pages.Queries.Properties
{
    /// <summary>
    /// Returns plain-text or limited HTML extracts of the given pages.
    /// <c>action=query&amp;prop=extracts</c>
    /// (<a href="https://www.mediawiki.org/wiki/Extension:TextExtracts#API">mw:Extension:TextExtracts#API</a>)
    /// </summary>
    public class GeoCoordinatePropertyProvider : WikiPagePropertyProvider<GeoCoordinatePropertyGroup>
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

        /// <inheritdoc />
        public override GeoCoordinatePropertyGroup ParsePropertyGroup(JObject json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            return GeoCoordinatePropertyGroup.Create((JArray)json["coordinates"]);
        }

    }

    public class GeoCoordinatePropertyGroup : WikiPagePropertyGroup
    {
        private static readonly GeoCoordinatePropertyGroup Empty = new GeoCoordinatePropertyGroup();
        private static readonly GeoCoordinate[] emptyCoordinates = { };

        private IReadOnlyCollection<GeoCoordinate> _Coordinates;

        internal static GeoCoordinatePropertyGroup Create(JArray jcoordinates)
        {
            if (jcoordinates == null) return Empty;
            if (!jcoordinates.HasValues) return Empty;
            return new GeoCoordinatePropertyGroup(jcoordinates);
        }

        private GeoCoordinatePropertyGroup()
        {
            PrimaryCoordinate = GeoCoordinate.Empty;
            PrimaryDistance = 0;
            _Coordinates = emptyCoordinates;
        }

        private GeoCoordinatePropertyGroup(JArray jcoordinates)
        {
            if (jcoordinates != null && jcoordinates.HasValues)
            {
                var jprimary = jcoordinates.FirstOrDefault(c => c["primary"] != null);
                if (jprimary != null)
                {
                    PrimaryCoordinate = MediaWikiHelper.GeoCoordinateFromJson((JObject)jcoordinates.First);
                    PrimaryDistance = (int?)jcoordinates.First["dist"] ?? 0;
                }
                if (jprimary == null || jcoordinates.Count > 1)
                {
                    var coordinates = jcoordinates.Select(c => MediaWikiHelper.GeoCoordinateFromJson((JObject)c)).ToArray();
                    _Coordinates = new ReadOnlyCollection<GeoCoordinate>(coordinates);
                }
            }
        }

        /// <summary>
        /// Gets the primary geo-coordinate associated with the page.
        /// </summary>
        public GeoCoordinate PrimaryCoordinate { get; }

        public double PrimaryDistance { get; }

        /// <summary>
        /// Gets all the geo-coordinates (including primary and secondary ones) associated with the page.
        /// </summary>
        public IReadOnlyCollection<GeoCoordinate> Coordinates
        {
            get
            {
                if (_Coordinates != null) return _Coordinates;
                if (PrimaryCoordinate.IsEmpty) _Coordinates = emptyCoordinates;
                _Coordinates = new ReadOnlyCollection<GeoCoordinate>(new[] {PrimaryCoordinate});
                return _Coordinates;
            }
        }
    }

}
