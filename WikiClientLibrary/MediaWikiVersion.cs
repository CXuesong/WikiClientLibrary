using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary
{

    /// <summary>
    /// MediaWiki development channels.
    /// </summary>
    /// <seealso cref="MediaWikiVersion"/>
    /// <seealso cref="SiteInfo.Version"/>
    public enum MediaWikiDevChannel
    {
        /// <summary>
        /// Official release. No version suffix.
        /// </summary>
        None = 0,
        /// <summary>
        /// WMF weekly release. Version suffix is <c>-wmf</c>.
        /// </summary>
        Wmf = 1,
        /// <summary>
        /// Alpha release. Version suffix is <c>-alpha</c>.
        /// </summary>
        Alpha = 2,
        /// <summary>
        /// Beta release. Version suffix is <c>-beta</c>.
        /// </summary>
        Beta = 3,
        /// <summary>
        /// Release candidate. Version suffix is <c>-rc</c>.
        /// </summary>
        RC = 4
    }

    /// <summary>
    /// Represents a MediaWiki core version number.
    /// </summary>
    /// <remarks>
    /// <para>MediaWiki version number has the following format</para>
    /// <para><var>major</var>.<var>minor</var>.<var>revision</var>[-<var>devChannel</var>[.<var>devVersion</var>]]</para>
    /// <para>where <var>devChannel</var> can be, in the order of release, <c>wmf</c>, <c>alpha</c>, <c>beta</c>, and <c>rc</c>.</para>
    /// </remarks>
    /// <seealso cref="MediaWikiDevChannel"/>
    /// <seealso cref="SiteInfo.Version"/>
    public struct MediaWikiVersion : IEquatable<MediaWikiVersion>, IComparable<MediaWikiVersion>, IComparable
    {

        private static readonly char[] dashCharArray = { '-' };
        private static readonly char[] dotCharArray = { '.' };

        private readonly short _Major, _Minor, _Revision;

        // bitmap for -wmf.3
        // DevChannel | DevVersion
        //       0001 | 0000 0000 0011
        private readonly short fullDevVersion;

        /// <summary>
        /// Gets the default zero value of <see cref="MediaWikiVersion"/>, i.e. <c>v0.0.0</c>.
        /// </summary>
        public static MediaWikiVersion Zero { get; } = default;

        /// <summary>
        /// Parses a MediaWiki core version number from its string representation.
        /// </summary>
        /// <param name="version">The version to be parsed.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="version"/> is not a valid version expression, such as
        /// <list type="bullet">
        /// <item><description>is <c>null</c>, empty, or whitespace.</description></item>
        /// <item><description>has more than 3 version components before the first dash, if any.</description></item>
        /// <item><description>has invalid dev-channel suffix. See <seealso cref="MediaWikiDevChannel"/> for a list of valid dev-channel suffixes.</description></item>
        /// <item><description>has invalid numeric version components, including failure to parse as number, or arithmetic overflow.</description></item>
        /// </list>
        /// </exception>
        /// <returns>The parsed version.</returns>
        /// <seealso cref="TryParse"/>
        public static MediaWikiVersion Parse(string version)
        {
            var result = Parse(version, true, out var v);
            Debug.Assert(result);
            return v;
        }

        /// <summary>
        /// Tries to parse a MediaWiki core version number from its string representation.
        /// </summary>
        /// <param name="version">The version to be parsed.</param>
        /// <param name="parsed">A variable to receive the parsed version value.</param>
        /// <returns>Whether the parsing is successful.</returns>
        /// <seealso cref="Parse(string)"/>
        public static bool TryParse(string version, out MediaWikiVersion parsed)
        {
            return Parse(version, false, out parsed);
        }

        public static bool Parse(string version, bool raiseError, out MediaWikiVersion parsed)
        {
            if (string.IsNullOrEmpty(version))
            {
                if (!raiseError) goto SILENT_FAIL;
                throw new ArgumentException(Prompts.ExceptionArgumentNullOrEmpty, nameof(version));
            }
            // 1.0.0-rc-1
            var components1 = version.Split(dashCharArray, 2);
            var components2 = components1[0].Split(dotCharArray, 4);
            if (components2.Length > 3)
            {
                if (!raiseError) goto SILENT_FAIL;
                throw new ArgumentException(Prompts.ExceptionVersionTooManyComponents, nameof(version));
            }
            short major;
            short minor = 0, revision = 0;
            var devChannel = MediaWikiDevChannel.None;
            var devVersion = 0;
            try
            {
                major = short.Parse(components2[0]);
                if (components2.Length > 1)
                    minor = short.Parse(components2[1]);
                if (components2.Length > 2)
                    revision = short.Parse(components2[2]);
            }
            catch (Exception ex)
            {
                if (!raiseError) goto SILENT_FAIL;
                throw new ArgumentException(Prompts.ExceptionVersionInvalidNumber, nameof(version), ex);
            }

            if (components1.Length > 1)
            {
                var devFullVersion = components1[1];
                int devVersionStartsAt;
                // Parse prefix.
                if (devFullVersion.StartsWith("wmf", StringComparison.OrdinalIgnoreCase))
                {
                    devChannel = MediaWikiDevChannel.Wmf;
                    devVersionStartsAt = 3;
                }
                else if (devFullVersion.StartsWith("rc", StringComparison.OrdinalIgnoreCase))
                {
                    devChannel = MediaWikiDevChannel.RC;
                    devVersionStartsAt = 2;
                }
                else if (devFullVersion.StartsWith("alpha", StringComparison.OrdinalIgnoreCase))
                {
                    devChannel = MediaWikiDevChannel.Alpha;
                    devVersionStartsAt = 5;
                }
                else if (devFullVersion.StartsWith("beta", StringComparison.OrdinalIgnoreCase))
                {
                    devChannel = MediaWikiDevChannel.Beta;
                    devVersionStartsAt = 4;
                }
                else
                {
                    if (!raiseError) goto SILENT_FAIL;
                    throw new ArgumentException(string.Format(Prompts.ExceptionVersionUnknownDevVersionPrefix1, devFullVersion));
                }
                // Parse version.
                if (devFullVersion.Length == devVersionStartsAt)
                {
                    devVersion = 0;
                }
                else
                {
                    if (devFullVersion[devVersionStartsAt] == '.' || devFullVersion[devVersionStartsAt] == '-')
                        devVersionStartsAt++;
                    // If . or - detected, we expect a number after it.
                    if (devFullVersion.Length <= devVersionStartsAt)
                    {
                        if (!raiseError) goto SILENT_FAIL;
                        throw new ArgumentException(Prompts.ExceptionVersionIncompleteDevVersion, nameof(version));
                    }
                    try
                    {
                        devVersion = int.Parse(devFullVersion.Substring(devVersionStartsAt));
                    }
                    catch (Exception ex)
                    {
                        if (!raiseError) goto SILENT_FAIL;
                        throw new ArgumentException(Prompts.ExceptionVersionInvalidDevVersion, nameof(version), ex);
                    }
                }
            }
            parsed = new MediaWikiVersion(major, minor, revision, devChannel, devVersion);
            return true;
            SILENT_FAIL:
            parsed = default;
            return false;
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

        /// <summary>
        /// Initializes a new <seealso cref="MediaWikiVersion"/> instance with major and minor versions.
        /// </summary>
        /// <inheritdoc cref="MediaWikiVersion(int,int,int,MediaWikiDevChannel,int)"/>
        public MediaWikiVersion(int major, int minor)
            : this(major, minor, 0, MediaWikiDevChannel.None, 0)
        {
        }

        /// <summary>
        /// Initializes a new <seealso cref="MediaWikiVersion"/> instance with major, minor, revision versions.
        /// </summary>
        /// <inheritdoc cref="MediaWikiVersion(int,int,int,MediaWikiDevChannel,int)"/>
        public MediaWikiVersion(int major, int minor, int revision)
            : this(major, minor, revision, MediaWikiDevChannel.None, 0)
        {
        }

        /// <summary>
        /// Initializes a new <seealso cref="MediaWikiVersion"/> instance with major, minor, revision versions and
        /// dev-channel.
        /// </summary>
        /// <inheritdoc cref="MediaWikiVersion(int,int,int,MediaWikiDevChannel,int)"/>
        public MediaWikiVersion(int major, int minor, int revision, MediaWikiDevChannel devChannel)
            : this(major, minor, revision, devChannel, 0)
        {
        }

        /// <summary>
        /// Initializes a new <seealso cref="MediaWikiVersion"/> instance with major, minor versions and
        /// dev-version.
        /// </summary>
        /// <inheritdoc cref="MediaWikiVersion(int,int,int,MediaWikiDevChannel,int)"/>
        public MediaWikiVersion(int major, int minor, MediaWikiDevChannel devChannel, int devVersion)
            : this(major, minor, 0, devChannel, devVersion)
        {
        }

        /// <summary>
        /// Initializes a new <seealso cref="MediaWikiVersion"/> instance with major, minor, revision versions and
        /// dev-version.
        /// </summary>
        /// <param name="major">Major version. Should be between 0 and 32767.</param>
        /// <param name="minor">Minor version. Should be between 0 and 32767.</param>
        /// <param name="revision">Revision number. Should be between 0 and 32767.</param>
        /// <param name="devChannel">Channel of development.</param>
        /// <param name="devVersion">DevVersion. Should be between 0 and 4095.</param>
        /// <exception cref="ArgumentOutOfRangeException">Any one of the version numbers is out of range.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="devChannel"/> is <see cref="MediaWikiDevChannel.None"/>,
        /// but <paramref name="devVersion"/> is non-zero.
        /// </exception>
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
                    throw new ArgumentException(Prompts.ExceptionDevVersionRequiresDevChannel, nameof(devVersion));
                fullDevVersion = 0;
            }
            else
            {
                fullDevVersion = (short)(((int)devChannel << 12) | devVersion);
            }
        }

        /// <summary>
        /// Major version.
        /// </summary>
        public int Major => _Major;

        /// <summary>
        /// Minor version.
        /// </summary>
        public int Minor => _Minor;

        /// <summary>
        /// Revision number.
        /// </summary>
        public int Revision => _Revision;

        /// <summary>
        /// Development channel.
        /// </summary>
        public MediaWikiDevChannel DevChannel => (MediaWikiDevChannel)(fullDevVersion >> 12);

        /// <summary>
        /// Version in development channel.
        /// </summary>
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
