using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace WikiClientLibrary
{

    public enum MediaWikiDevChannel
    {
        None = 0,
        Wmf = 1,
        Alpha = 2,
        Beta = 3,
        RC = 4
    }

    /// <summary>
    /// Represents a MediaWiki core version number.
    /// </summary>
    /// <remarks>
    /// <para>MediaWiki version number has the following format</para>
    /// <para><var>major</var>.<var>minor</var>.<var>revision</var>[-<var>devChannel</var>.<var>devVersion</var>]</para>
    /// </remarks>
    public struct MediaWikiVersion : IEquatable<MediaWikiVersion>, IComparable<MediaWikiVersion>, IComparable
    {

        private static readonly char[] dashCharArray = { '-' };
        private static readonly char[] dotCharArray = { '.' };

        private readonly short _Major, _Minor, _Revision;
        // bitmap for -wmf.3
        // DevChannel | DevVersion
        //       0001 | 0000 0000 0011
        private readonly short fullDevVersion;

        public static MediaWikiVersion Zero { get; } = default;

        public static MediaWikiVersion Parse(string version)
        {
            if (string.IsNullOrEmpty(version))
                throw new ArgumentException("Value cannot be null or empty.", nameof(version));
            // 1.0.0-rc-1
            var components1 = version.Split(dashCharArray, 2);
            var components2 = components1[0].Split(dotCharArray, 4);
            if (components2.Length > 3)
                throw new ArgumentException("Version has too many components.", nameof(version));
            var major = short.Parse(components2[0]);
            short minor = 0, revision = 0;
            var devChannel = MediaWikiDevChannel.None;
            var devVersion = 0;
            if (components2.Length > 1)
                minor = short.Parse(components2[1]);
            if (components2.Length > 2)
                revision = short.Parse(components2[2]);

            int ParseDevVersion(string expr, int startsFrom)
            {
                if (expr.Length == startsFrom)
                    return 0;
                if (expr[startsFrom] == '.' || expr[startsFrom] == '-')
                    startsFrom++;
                // If . or - detected, we expect a number after it.
                if (expr.Length <= startsFrom)
                    throw new ArgumentException("Incomplete DevVersion expression.", nameof(version));
                return int.Parse(expr.Substring(startsFrom));
            }
            if (components1.Length > 1)
            {
                var devFullVersion = components1[1];
                if (devFullVersion.StartsWith("wmf", StringComparison.OrdinalIgnoreCase))
                {
                    devChannel = MediaWikiDevChannel.Wmf;
                    devVersion = ParseDevVersion(devFullVersion, 3);
                }
                else if (devFullVersion.StartsWith("rc", StringComparison.OrdinalIgnoreCase))
                {
                    devChannel = MediaWikiDevChannel.RC;
                    devVersion = ParseDevVersion(devFullVersion, 2);
                }
                else if (devFullVersion.StartsWith("alpha", StringComparison.OrdinalIgnoreCase))
                {
                    devChannel = MediaWikiDevChannel.Alpha;
                    devVersion = ParseDevVersion(devFullVersion, 5);
                }
                else if (devFullVersion.StartsWith("beta", StringComparison.OrdinalIgnoreCase))
                {
                    devChannel = MediaWikiDevChannel.Beta;
                    devVersion = ParseDevVersion(devFullVersion, 4);
                }
                else
                {
                    throw new ArgumentException($"Unrecognizable DevVersion prefix: {devFullVersion}.");
                }
            }
            return new MediaWikiVersion(major, minor, revision, devChannel, devVersion);
        }

        private MediaWikiVersion(short major, short minor, short revision, MediaWikiDevChannel devChannel, int devVersion)
        {
            if (major < 0)
                throw new ArgumentOutOfRangeException(nameof(major));
            if (minor < 0)
                throw new ArgumentOutOfRangeException(nameof(minor));
            if (revision < 0)
                throw new ArgumentOutOfRangeException(nameof(revision));
            if (devVersion < 0 || devVersion > 0x0FFF)
                throw new ArgumentOutOfRangeException(nameof(devVersion));
            _Major = major;
            _Minor = minor;
            _Revision = revision;
            fullDevVersion = (short)(((int)devChannel << 12) | devVersion);
        }
        public MediaWikiVersion(int major, int minor) 
            : this(major, minor, 0, MediaWikiDevChannel.None, 0)
        {
        }

        public MediaWikiVersion(int major, int minor, int revision) 
            : this(major, minor, revision, MediaWikiDevChannel.None, 0)
        {
        }

        public MediaWikiVersion(int major, int minor, int revision, MediaWikiDevChannel devChannel) 
            : this(major, minor, revision, devChannel, 0)
        {
        }

        public MediaWikiVersion(int major, int minor, int revision, MediaWikiDevChannel devChannel, int devVersion)
        {
            if (major < 0 || major > short.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(major));
            if (minor < 0 || minor > short.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(minor));
            if (revision < 0 || revision > short.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(revision));
            if (devChannel < MediaWikiDevChannel.None || devChannel > MediaWikiDevChannel.RC)
                throw new ArgumentOutOfRangeException(nameof(devChannel));
            if (devVersion < 0 || devVersion > 0x0FFF)
                throw new ArgumentOutOfRangeException(nameof(devVersion));
            _Major = (short)major;
            _Minor = (short)minor;
            _Revision = (short)revision;
            if (devChannel == MediaWikiDevChannel.None)
            {
                if (devVersion != 0)
                    throw new ArgumentException("When devChannel is None, devVersion must be 0.", nameof(devVersion));
                fullDevVersion = 0;
            }
            else
            {
                fullDevVersion = (short)(((int)devChannel << 12) | devVersion);
            }
        }

        public int Major => _Major;

        public int Minor => _Minor;

        public int Revision => _Revision;

        public MediaWikiDevChannel DevChannel => (MediaWikiDevChannel)(fullDevVersion >> 12);

        public int DevVersion => fullDevVersion & 0x0FFF;

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return unchecked(_Major * 113 + _Minor * 57 + _Revision * 23 + fullDevVersion);
        }


        /// <inheritdoc />
        public int CompareTo(MediaWikiVersion other)
        {
            if (_Major > other._Major) return 1;
            if (_Major < other._Major) return -1;
            if (_Minor > other._Minor) return 1;
            if (_Minor < other._Minor) return -1;
            if (_Revision > other._Revision) return 1;
            if (_Revision < other._Revision) return -1;
            // fullDevVersion != 0: wmf/alpha/beta/rc
            if (fullDevVersion == 0)
                return other.fullDevVersion == 0 ? 0 : 1;
            if (other.fullDevVersion == 0)
                // We have fullDevVersion != 0 here.
                return -1;
            if (fullDevVersion > other.fullDevVersion) return 1;
            if (fullDevVersion < other.fullDevVersion) return -1;
            return 0;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var sb = new StringBuilder(20);
            sb.Append(Major);
            sb.Append('.');
            sb.Append(Minor);
            sb.Append('.');
            sb.Append(Revision);
            var channel = DevChannel;
            if (channel != MediaWikiDevChannel.None)
            {
                switch (DevChannel)
                {
                    case MediaWikiDevChannel.Wmf:
                        sb.Append("-wmf");
                        break;
                    case MediaWikiDevChannel.Alpha:
                        sb.Append("-alpha");
                        break;
                    case MediaWikiDevChannel.Beta:
                        sb.Append("-beta");
                        break;
                    case MediaWikiDevChannel.RC:
                        sb.Append("-rc");
                        break;
                    default:
                        Debug.Assert(false, "Invalid DevChannel value.");
                        sb.Append("-invalid");
                        break;
                }
                var devVersion = DevVersion;
                if (devVersion > 0)
                {
                    sb.Append('.');
                    sb.Append(devVersion);
                }
            }
            return sb.ToString();
        }

        /// <inheritdoc />
        public bool Equals(MediaWikiVersion other)
        {
            return _Major == other._Major && _Minor == other._Minor && _Revision == other._Revision && fullDevVersion == other.fullDevVersion;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is MediaWikiVersion other && Equals(other);
        }

        public static bool operator ==(MediaWikiVersion left, MediaWikiVersion right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MediaWikiVersion left, MediaWikiVersion right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc />
        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj)) return 1;
            return obj is MediaWikiVersion other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(MediaWikiVersion)}");
        }

        public static bool operator <(MediaWikiVersion left, MediaWikiVersion right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator >(MediaWikiVersion left, MediaWikiVersion right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator <=(MediaWikiVersion left, MediaWikiVersion right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >=(MediaWikiVersion left, MediaWikiVersion right)
        {
            return left.CompareTo(right) >= 0;
        }
    }
}
