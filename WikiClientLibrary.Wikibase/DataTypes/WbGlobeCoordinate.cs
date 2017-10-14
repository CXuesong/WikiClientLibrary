using System;

namespace WikiClientLibrary.Wikibase.DataTypes
{
    /// <summary>Represents a point on the globe.</summary>
    public struct WbGlobeCoordinate : IEquatable<WbGlobeCoordinate>
    {

        /// <summary>
        /// Initialize a new <see cref="WbGlobeCoordinate"/> instance with coordinate, precision, and globe entity URI.
        /// </summary>
        /// <param name="latitude">Latitude, in degrees.</param>
        /// <param name="longitude">Longitude, in degrees.</param>
        /// <param name="precision">Precision, in degrees.</param>
        /// <param name="globe">Entity URI of the globe.</param>
        public WbGlobeCoordinate(double latitude, double longitude, double precision, WbUri globe)
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

        /// <summary>Entity URI of the globe.</summary>
        public WbUri Globe { get; }

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
        public bool Equals(WbGlobeCoordinate other)
        {
            return Latitude.Equals(other.Latitude) && Longitude.Equals(other.Longitude)
                   && Precision.Equals(other.Precision) && Globe == other.Globe;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is WbGlobeCoordinate coordinate && Equals(coordinate);
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

        public static bool operator ==(WbGlobeCoordinate left, WbGlobeCoordinate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WbGlobeCoordinate left, WbGlobeCoordinate right)
        {
            return !left.Equals(right);
        }
    }
}
