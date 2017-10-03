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

        /// <summary>Latitude, in degrees.</summary>
        public double Latitude { get; }

        /// <summary>Longitude, in degrees.</summary>
        public double Longitude { get; }

        /// <summary>Precision, in degrees.</summary>
        public double Precision { get; }

        /// <summary>Item URI of the globe.</summary>
        public WikibaseUri Globe { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            var s = Latitude + "°";
            if (Latitude >= 0) s += "°N ";
            else s += "°S ";
            s += Longitude;
            if (Longitude >= 0) s += "°E";
            else s += "°W";
            return s;
        }

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
