using System;
using System.Collections.Generic;
using System.Text;

namespace WikiClientLibrary
{
    /// <summary>
    /// Represents a coordinate and an optional size in radius on the globe.
    /// </summary>
    /// <remarks>This structure uses degrees as the unit of the coordinate.</remarks>
    public struct GeoCoordinate : IEquatable<GeoCoordinate>
    {

        /// <summary>
        /// The globe identifier of the Earth.
        /// </summary>
        /// <seealso cref="Globe"/>
        public const string Earth = "earth";

        /// <summary>
        /// Gets an <see cref="GeoCoordinate"/> value with its members uninitialized.
        /// </summary>
        public static readonly GeoCoordinate Empty = new GeoCoordinate();

        /// <summary>Initializes a new instance of <see cref="GeoCoordinate"/> representing a location on the Earth.</summary>
        /// <inheritdoc cref="GeoCoordinate(double,double,double,string)"/>
        /// <remarks>The <see cref="Dimension"/> is set to <c>0</c>.</remarks>
        public GeoCoordinate(double latitude, double longitude) : this(latitude, longitude, 0, Earth)
        {
        }

        /// <summary>Initializes a new instance of <see cref="GeoCoordinate"/> representing a location on the Earth.</summary>
        /// <inheritdoc cref="GeoCoordinate(double,double,double,string)"/>
        public GeoCoordinate(double latitude, double longitude, double dimension) : this(latitude, longitude, dimension, Earth)
        {
        }

        /// <param name="latitude">latitude of the location.</param>
        /// <param name="longitude">longitude of the location.</param>
        /// <param name="dimension">size of the object.</param>
        /// <param name="globe">globe identifier of the coordinate.</param>
        public GeoCoordinate(double latitude, double longitude, double dimension, string globe)
        {
            Longitude = longitude;
            Latitude = latitude;
            Dimension = dimension;
            Globe = globe;
        }

        /// <summary>
        /// Gets/sets the latitude of the location.
        /// </summary>
        public double Latitude { get; set; }

        /// <summary>
        /// Gets/sets the longitude of the location.
        /// </summary>
        public double Longitude { get; set; }

        /// <summary>
        /// Gets/sets the size of the object.
        /// </summary>
        public double Dimension { get; set; }

        /// <summary>
        /// Gets/set the globe identifier of the coordinate.
        /// </summary>
        /// <seealso cref="Earth"/>
        public string? Globe { get; set; }

        /// <summary>
        /// Gets a value that indicates if all the members in the structure have their uninitialized values.
        /// </summary>
        public bool IsEmpty => Globe == null && Longitude.Equals(0) && Latitude.Equals(0) && Dimension.Equals(0);

        /// <summary>
        /// Determines whether the current coordinates is normalized.
        /// </summary>
        /// <remarks>
        /// A normalized coordinate has <see cref="Longitude"/> between -180 ~ +180,
        /// and <see cref="Latitude"/> between -90 ~ 90.
        /// </remarks>
        public bool IsNormalized => Longitude >= -180 && Longitude <= 180 && Latitude >= -90 && Latitude <= 90;

        /// <summary>
        /// Offsets the coordinates by the specified values.
        /// </summary>
        /// <param name="longitudeOffset">Offset of the longitude.</param>
        /// <param name="latitudeOffset">Offset of the latitude.</param>
        public void Offset(double longitudeOffset, double latitudeOffset)
        {
            Longitude += longitudeOffset;
            Latitude += latitudeOffset;
        }

        /// <summary>
        /// Normalizes the coordinates, ensuring the current coordinates meets
        /// the definition of "normalized coordinates" as specified in <see cref="IsNormalized"/>.
        /// </summary>
        public void Normalize()
        {
            if (Longitude < -180 || Longitude > 180)
            {
                var lm = Math.IEEERemainder(Longitude, 360);
                if (lm > 180) lm -= 360;
                else if (lm < -180) lm += 360;
                Longitude = lm;
            }
            if (Latitude < -180 || Latitude > 180)
            {
                var lm = Math.IEEERemainder(Latitude, 180);
                if (lm > 90) lm -= 180;
                else if (lm < -90) lm += 180;
                Latitude = lm;
            }
        }

        internal static string ToString(double longitude, double latitude)
        {
            var s = latitude.ToString();
            if (latitude >= 0) s += "°N ";
            else s += "°S ";
            s += longitude;
            if (longitude >= 0) s += "°E";
            else s += "°W";
            return s;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return ToString(Longitude, Latitude);
        }

        /// <inheritdoc />
        public bool Equals(GeoCoordinate other)
        {
            return Longitude.Equals(other.Longitude) && Latitude.Equals(other.Latitude) && Dimension.Equals(other.Dimension) && string.Equals(Globe, other.Globe);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            if (obj is null) return false;
            return obj is GeoCoordinate coordinate && Equals(coordinate);
        }

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(Longitude, Latitude, Dimension, Globe);

        public static bool operator ==(GeoCoordinate left, GeoCoordinate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GeoCoordinate left, GeoCoordinate right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Represents a spherical rectangle determined by the top-left (north-west) and bottom-right (south-east) coordinates on the globe.
    /// </summary>
    /// <remarks>
    /// <para>This structure uses degrees as the unit of the coordinate.</para>
    /// <para>
    /// Keep in mind the latitude decreases as you go down / south (i.e. bottom of the rectangle).
    /// Thus <see cref="Bottom"/> value is less than or equals to <see cref="Top"/> value.
    /// </para>
    /// <para>
    /// Rectangles spanning along meridian and across the north/south pole may not be represented properly by this structure.
    /// Consider using two <see cref="GeoCoordinateRectangle"/> instances in this case.
    /// Though you may still represent such a rectangle with <see cref="Bottom"/> less than -90,
    /// you will not be able to normalize it and perform other common operations.
    /// See <see cref="Normalize"/> and <see cref="Contains"/> for more information.
    /// </para>
    /// </remarks>
    public struct GeoCoordinateRectangle : IEquatable<GeoCoordinateRectangle>
    {

        /// <summary>
        /// Gets an empty <see cref="GeoCoordinateRectangle"/> value with its members uninitialized.
        /// </summary>
        public static readonly GeoCoordinateRectangle Empty = new GeoCoordinateRectangle();

        private double _Width;
        private double _Height;

        /// <summary>
        /// Constructs a <see cref="GeoCoordinateRectangle" /> instance from the given bounding coordinates.
        /// </summary>
        /// <param name="longitude1">first longitude of the rectangle.</param>
        /// <param name="latitude1">first latitude of the rectangle.</param>
        /// <param name="longitude2">second longitude of the rectangle.</param>
        /// <param name="latitude2">second latitude of the rectangle.</param>
        /// <returns>
        /// a <see cref="GeoCoordinateRectangle"/> that uses the given vertex coordinates.
        /// First longitude/latitude may be swapped with second one to ensure the resulting rectangle has
        /// positive <see cref="Width"/> and <see cref="Height"/>.
        /// </returns>
        public static GeoCoordinateRectangle FromBoundingCoordinates(double longitude1, double latitude1, double longitude2, double latitude2)
        {
            return new GeoCoordinateRectangle(
                Math.Min(longitude1, longitude2), Math.Max(latitude1, latitude2),
                Math.Abs(longitude1 - longitude2), Math.Abs(latitude1 - latitude2)
            );
        }

        /// <summary>
        /// Constructs a <see cref="GeoCoordinateRectangle" /> instance from the given bounding coordinates.
        /// </summary>
        /// <param name="coordinate1">first vertex coordinate. <see cref="GeoCoordinate.Dimension"/> is ignored.</param>
        /// <param name="coordinate2">second vertex coordinate. <see cref="GeoCoordinate.Dimension"/> is ignored.</param>
        public static GeoCoordinateRectangle FromBoundingCoordinates(GeoCoordinate coordinate1, GeoCoordinate coordinate2)
        {
            return new GeoCoordinateRectangle(
                Math.Min(coordinate1.Longitude, coordinate2.Longitude), Math.Max(coordinate1.Latitude, coordinate2.Latitude),
                Math.Abs(coordinate1.Longitude - coordinate2.Longitude), Math.Abs(coordinate1.Latitude - coordinate2.Latitude)
            );
        }

        /// <summary>
        /// Initializes a new <see cref="GeoCoordinateRectangle" /> with the specified left, top, width, height on sphere.
        /// </summary>
        /// <param name="left">left-border longitude of the rectangle.</param>
        /// <param name="top">top-border latitude of the rectangle.</param>
        /// <param name="width">width in longitude of the rectangle.</param>
        /// <param name="height">height in latitude of the rectangle.</param>
        /// <exception cref="ArgumentOutOfRangeException">Either <paramref name="width"/> or <paramref name="height"/> is negative.</exception>
        /// <seealso cref="FromBoundingCoordinates(double,double,double,double)"/>
        /// <seealso cref="FromBoundingCoordinates(GeoCoordinate,GeoCoordinate)"/>
        public GeoCoordinateRectangle(double left, double top, double width, double height)
        {
            if (width < 0) throw new ArgumentOutOfRangeException(nameof(width), width, Prompts.ExceptionArgumentIsNegative);
            if (height < 0) throw new ArgumentOutOfRangeException(nameof(height), height, Prompts.ExceptionArgumentIsNegative);
            this.Left = left;
            this.Top = top;
            this._Width = width;
            this._Height = height;
        }

        /// <summary>
        /// Gets/sets the left-border longitude of the rectangle.
        /// </summary>
        public double Left { get; set; }

        /// <summary>
        /// Gets/sets the top-border latitude of the rectangle.
        /// </summary>
        public double Top { get; set; }

        /// <summary>
        /// Gets/sets the width in longitude of the rectangle.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
        public double Width
        {
            get
            {
                return _Width;
            }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value, Prompts.ExceptionArgumentIsNegative);
                _Width = value;
            }
        }

        /// <summary>
        /// Gets/sets the height in latitude of the rectangle.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
        public double Height
        {
            get
            {
                return _Height;
            }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value, Prompts.ExceptionArgumentIsNegative);
                _Height = value;
            }
        }

        /// <summary>
        /// Gets the right-border longitude of the rectangle.
        /// </summary>
        public double Right => this.Left + this._Width;

        /// <summary>
        /// Gets the bottom-border latitude of the rectangle.
        /// </summary>
        /// <para>
        /// Keep in mind the latitude decreases as you go down / south (i.e. bottom of the rectangle).
        /// Thus <see cref="Bottom"/> value is less than or equals to <see cref="Top"/> value.
        /// </para>
        public double Bottom => this.Top - this._Height;

        /// <summary>
        /// Determines whether the rectangle is empty in area.
        /// </summary>
        public bool IsEmpty => _Width <= 0 || _Height <= 0;

        /// <summary>
        /// Determines whether the coordinates of current spherical rectangle are normalized.
        /// </summary>
        /// <remarks>
        /// <para>"Normalized rectangle" provides a way to make all the effectively same rectangles have the same coordinates.</para>
        /// <para>A normalized rectangle has
        /// <see cref="Left"/> between -180 ~ +180,
        /// <see cref="Top"/> between -90 ~ +90,
        /// <see cref="Width"/> between 0 ~ 360,
        /// and <see cref="Height"/> between 0 ~ 180.
        /// Taking account to the limitation of rectangles that is representable by this structure,
        /// the normalized rectangle should also have
        /// <see cref="Right"/> between -180 ~ +540 (-180 ~ 180 if the rectangle does not cross anti-meridian),
        /// and <see cref="Bottom"/> between -90 ~ +90.
        /// </para>
        /// <para>For "belts"/"semi-belts" along meridian and/or parallel,
        /// <list type="bullet">
        /// <item><description>if <see cref="Width"/> is 360, then <see cref="Left"/> should be 0;</description></item>
        /// <item><description>if <see cref="Height"/> is 180, then <see cref="Top"/> should be 90.</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        // TODO Still need to address the case where the rectangle spans along meridian and across the north/south pole.
        public bool IsNormalized => IsNormalizable
                                    && Left >= -180 && Left <= 180 && Top >= -90 && Top <= 90
                                    && (_Width < 360 || _Width.Equals(360) && Left.Equals(0))
                                    && (_Height < 180 || _Height.Equals(180) && Top.Equals(90));

        /// <summary>
        /// Gets a value that indicates whether the current rectangle instance is normalizable.
        /// </summary>
        /// <remarks>
        /// A normalizable rectangle should have
        /// <see cref="Top"/> between <c>n*360 + (-90 ~ +90)</c>
        /// and <see cref="Bottom"/> less than or equal to <c>n*360 + 90</c>.
        /// </remarks>
        public bool IsNormalizable
        {
            get
            {
                var normalizedTop = Math.IEEERemainder(this.Top, 360);
                return normalizedTop >= -90 && normalizedTop - this._Height <= 90;
            }
        } 

        /// <summary>
        /// Determines whether the rectangle contains the given coordinate,
        /// or the given coordinate falls on the edge of the rectangle.
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="IsNormalizable"/> is false. This operation cannot be performed when the rectangle is not normalizable.</exception>
        public bool Contains(GeoCoordinate coordinate)
        {
            var nr = this;
            nr.Normalize();
            coordinate.Normalize();
            if (nr.Top < coordinate.Latitude || nr.Bottom > coordinate.Latitude) return false;
            // assume the arc does not cross anti-meridian
            if (nr.Left <= coordinate.Longitude && nr.Right >= coordinate.Longitude) return true;
            // The arc crosses anti-meridian
            var l = coordinate.Longitude + 360;
            if (nr.Left <= l && nr.Right >= l) return true;
            return false;
        }

        /// <summary>
        /// Offsets the rectangle by the specified values.
        /// </summary>
        /// <param name="longitudeOffset">Offset of the longitude.</param>
        /// <param name="latitudeOffset">Offset of the latitude.</param>
        public void Offset(double longitudeOffset, double latitudeOffset)
        {
            Left += longitudeOffset;
            Top += latitudeOffset;
        }

        /// <summary>
        /// Normalizes the coordinates, ensuring the current coordinates meets
        /// the definition of "normalized spherical rectangle" as specified in <see cref="IsNormalized"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="IsNormalizable"/> is false. That is, trying to normalize a non-normalizable rectangle.</exception>
        public void Normalize()
        {
            if (!IsNormalizable)
                throw new InvalidOperationException(Prompts.ExceptionGeoCoordinateRectangleNotNormalizable);
            if (_Width >= 360)
            {
                // Belt along parallel.
                Left = 0;
                _Width = 360;
            }
            else if (Left < -180 || Left > 180)
            {
                var lm = Math.IEEERemainder(Left, 360);
                if (lm > 180) lm -= 360;
                else if (lm < -180) lm += 360;
                Left = lm;
            }
            if (_Height >= 360)
            {
                // Belt along meridian.
                Top = 0;
                _Height = 360;
            }
            if (Top < -90 || Top > 90)
            {
                var lm = Math.IEEERemainder(Top, 180);
                if (lm > 90) lm -= 180;
                else if (lm < -90) lm += 180;
                Top = lm;
            }
        }

        /// <inheritdoc />
        public bool Equals(GeoCoordinateRectangle other)
        {
            return _Width.Equals(other._Width) && _Height.Equals(other._Height) && Left.Equals(other.Left) && Top.Equals(other.Top);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is GeoCoordinateRectangle other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = _Width.GetHashCode();
                hashCode = (hashCode * 397) ^ _Height.GetHashCode();
                hashCode = (hashCode * 397) ^ Left.GetHashCode();
                hashCode = (hashCode * 397) ^ Top.GetHashCode();
                return hashCode;
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return GeoCoordinate.ToString(Left, Top) + $" W{_Width}° H{_Height}°";
        }

        public static bool operator ==(GeoCoordinateRectangle left, GeoCoordinateRectangle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GeoCoordinateRectangle left, GeoCoordinateRectangle right)
        {
            return !left.Equals(right);
        }

    }
}
