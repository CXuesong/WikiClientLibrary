using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using WikiClientLibrary.Pages;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// An item in the GeoSearch result.
    /// </summary>
    /// <seealso cref="GeoSearchGenerator"/>
    public sealed class GeoSearchResultItem
    {
        internal GeoSearchResultItem(WikiPageStub page, GeoCoordinate coordinate, bool isPrimaryCoordinate, double distance)
        {
            Page = page;
            Coordinate = coordinate;
            IsPrimaryCoordinate = isPrimaryCoordinate;
            Distance = distance;
        }

        /// <summary>
        /// Gets the object's associated page stub.
        /// </summary>
        public WikiPageStub Page { get; }

        /// <summary>
        /// Gets the coordinate of the object.
        /// </summary>
        public GeoCoordinate Coordinate { get; }

        /// <summary>
        /// Gets a value indicating whether the coordinate is the primary coordinate of the object.
        /// </summary>
        public bool IsPrimaryCoordinate { get; }

        /// <summary>
        /// Distance of the object from the search location, in meters.
        /// </summary>
        public double Distance { get; }

    }
}
