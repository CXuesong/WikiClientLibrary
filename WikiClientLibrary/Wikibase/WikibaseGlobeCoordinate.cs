using System;
using System.Collections.Generic;
using System.Text;

namespace WikiClientLibrary.Wikibase
{
    public struct WikibaseGlobeCoordinate : IEquatable<WikibaseGlobeCoordinate>
    {

        public WikibaseGlobeCoordinate(double latitude, double longitude, double precision, WikibaseUri globe)
        {
            Latitude = latitude;
            Longitude = longitude;
            Precision = precision;
            Globe = globe ?? throw new ArgumentNullException(nameof(globe));
        }

        public double Latitude { get; }

        public double Longitude { get; }

        public double Precision { get; }

        public WikibaseUri Globe { get; }

        /// <inheritdoc />
        public bool Equals(WikibaseGlobeCoordinate other)
        {
            return Latitude.Equals(other.Latitude) && Longitude.Equals(other.Longitude)
                   && Precision.Equals(other.Precision) && Globe == other.Globe;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is WikibaseGlobeCoordinate coordinate && Equals(coordinate);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Latitude.GetHashCode();
                hashCode = (hashCode * 397) ^ Longitude.GetHashCode();
                hashCode = (hashCode * 397) ^ Precision.GetHashCode();
                hashCode = (hashCode * 397) ^ (Globe != null ? Globe.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static bool operator ==(WikibaseGlobeCoordinate left, WikibaseGlobeCoordinate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WikibaseGlobeCoordinate left, WikibaseGlobeCoordinate right)
        {
            return !left.Equals(right);
        }
    }
}
