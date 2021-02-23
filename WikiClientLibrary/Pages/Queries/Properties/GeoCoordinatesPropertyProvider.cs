using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Pages.Queries.Properties
{
    /// <summary>
    /// Returns the geographical coordinates associated with the page.
    /// (<a href="https://www.mediawiki.org/wiki/Extension:GeoData#prop=coordinates">mw:Extension:GeoData#prop=coordinates</a>)
    /// </summary>
    public class GeoCoordinatesPropertyProvider : WikiPagePropertyProvider<GeoCoordinatesPropertyGroup>
    {

        /// <summary>
        /// Whether to query for primary coordinate for the page.
        /// </summary>
        /// <remarks>The default value is <c>true</c>.</remarks>
        public bool QueryPrimaryCoordinate { get; set; } = true;

        /// <summary>
        /// Whether to query for the secondary coordinate for the page.
        /// </summary>
        /// <remarks>The default value is <c>false</c>.</remarks>
        public bool QuerySecondaryCoordinate { get; set; }

        /// <summary>
        /// Also queries for the distance of each coordinate from the given point.
        /// </summary>
        /// <remarks>A valid coordinate, or <see cref="GeoCoordinate.Empty"/> if no distance querying is required.</remarks>
        /// <seealso cref="QueryDistanceFromPage"/>
        public GeoCoordinate QueryDistanceFromPoint { get; set; }

        /// <summary>
        /// Also queries for the distance of each coordinate from the coordinate of the given page.
        /// </summary>
        /// <remarks>A page title, or <c>null</c> if no distance querying is required.</remarks>
        /// <seealso cref="QueryDistanceFromPoint"/>
        public string QueryDistanceFromPage { get; set; }

        /// <inheritdoc />
        public override IEnumerable<KeyValuePair<string, object?>> EnumParameters(MediaWikiVersion version)
        {
            var p = new OrderedKeyValuePairs<string, object>
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
                throw new ArgumentException(string.Format(Prompts.ExceptionArgumentExpectEitherBothTrue2, nameof(QueryPrimaryCoordinate), nameof(QuerySecondaryCoordinate)));

            if (!QueryDistanceFromPoint.IsEmpty && QueryDistanceFromPage == null)
                throw new ArgumentException(string.Format(Prompts.ExceptionArgumentExpectEitherDefault2, nameof(QueryDistanceFromPoint), nameof(QueryDistanceFromPage)));
            if (!QueryDistanceFromPoint.IsEmpty)
                p.Add("codistancefrompoint", QueryDistanceFromPoint.Latitude + "|" + QueryDistanceFromPoint.Longitude);
            if (QueryDistanceFromPage != null)
                p.Add("codistancefrompage", QueryDistanceFromPage);

            return p;
        }

        /// <inheritdoc />
        public override string? PropertyName => "coordinates";

        /// <inheritdoc />
        public override GeoCoordinatesPropertyGroup ParsePropertyGroup(JObject json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            return GeoCoordinatesPropertyGroup.Create((JArray)json["coordinates"]);
        }

    }

    public class GeoCoordinatesPropertyGroup : WikiPagePropertyGroup
    {
        private static readonly GeoCoordinatesPropertyGroup Empty = new GeoCoordinatesPropertyGroup();

        private IReadOnlyCollection<GeoCoordinate> _Coordinates;

        internal static GeoCoordinatesPropertyGroup Create(JArray jcoordinates)
        {
            if (jcoordinates == null) return Empty;
            if (!jcoordinates.HasValues) return Empty;
            return new GeoCoordinatesPropertyGroup(jcoordinates);
        }

        private GeoCoordinatesPropertyGroup()
        {
            PrimaryCoordinate = GeoCoordinate.Empty;
            PrimaryDistance = 0;
            _Coordinates = Array.Empty<GeoCoordinate>();
        }

        private GeoCoordinatesPropertyGroup(JArray jcoordinates)
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
                if (PrimaryCoordinate.IsEmpty) _Coordinates = Array.Empty<GeoCoordinate>();
                _Coordinates = new ReadOnlyCollection<GeoCoordinate>(new[] { PrimaryCoordinate });
                return _Coordinates;
            }
        }
    }

}
