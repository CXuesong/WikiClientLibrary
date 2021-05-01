using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
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
    public readonly struct MediaWikiVersion : IEquatable<MediaWikiVersion>, IComparable<MediaWikiVersion>, IComparable
    {

        private static readonly Regex versionRegex = new Regex(@"
^\s*(?<Major>\d+)\s*
(\.\s*(?<Minor>\d+)\s*)?
(\.\s*(?<Revision>\d+)\s*)?
(
    -\s*(?<DevChannel>wmf|alpha|beta|rc)\s*
    ((\.|-)?\s*?(?<DevVersion>\d+))?
)?\s*
(?<UnknownSuffix>.+)?",
            RegexOptions.IgnorePatternWhitespace
            | RegexOptions.IgnoreCase
            | RegexOptions.CultureInvariant);

        private readonly short _Major, _Minor, _Revision;

        // bitmap for -wmf.3
        // DevChannel | DevVersion
        //       0001 | 0000 0000 0011
        private readonly ushort fullDevVersion;

        /// <summary>
        /// Gets the default zero value of <see cref="MediaWikiVersion"/>, i.e. <c>v0.0.0</c>.
        /// </summary>
        public static MediaWikiVersion Zero { get; } = default;

        /// <inheritdoc cref="Parse(string,bool)"/>
        /// <remarks>This overload does not allow version truncation.</remarks>
        public static MediaWikiVersion Parse(string version)
        {
            return Parse(version, false);
        }

        /// <summary>
        /// Parses a MediaWiki core version number from its string representation.
        /// </summary>
        /// <param name="version">The version to be parsed.</param>
        /// <param name="allowTruncation">Whether allows truncating unknown version suffix (such as <c>-1+deb7u1</c> in <c>1.19.5-1+deb7u1</c>).</param>
        /// <exception cref="ArgumentException"><paramref name="version"/> is <c>null</c> or empty.</exception>
        /// <exception cref="FormatException">
        /// <paramref name="version"/> is not a valid version expression, such as
        /// <list type="bullet">
        /// <item><description>is <c>null</c>, empty, or whitespace.</description></item>
        /// <item><description>has more than 3 version components before the first dash, if any.</description></item>
        /// <item><description>has invalid dev-channel suffix, and suffix truncation is not allowed. See <seealso cref="MediaWikiDevChannel"/> for a list of valid dev-channel suffixes.</description></item>
        /// <item><description>has invalid numeric version components, including failure to parse as number, or arithmetic overflow.</description></item>
        /// </list>
        /// </exception>
        /// <returns>The parsed version.</returns>
        /// <seealso cref="TryParse(string,out MediaWikiVersion)"/>
        public static MediaWikiVersion Parse(string version, bool allowTruncation)
        {
            var result = Parse(version, allowTruncation, true, out var v);
            Debug.Assert(result);
            return v;
        }

        /// <inheritdoc cref="TryParse(string,bool,out MediaWikiVersion)"/>
        /// <remarks>This overload does not allow version truncation.</remarks>
        public static bool TryParse(string version, out MediaWikiVersion parsed)
        {
            return Parse(version, false, false, out parsed);
        }

        /// <summary>
        /// Tries to parse a MediaWiki core version number from its string representation.
        /// </summary>
        /// <param name="version">The version to be parsed.</param>
        /// <param name="allowTruncation">Whether allows truncating unknown version suffix (such as <c>-1+deb7u1</c> in <c>1.19.5-1+deb7u1</c>).</param>
        /// <param name="parsed">A variable to receive the parsed version value.</param>
        /// <returns>Whether the parsing is successful.</returns>
        /// <seealso cref="Parse(string)"/>
        public static bool TryParse(string version, bool allowTruncation, out MediaWikiVersion parsed)
        {
            return Parse(version, true, false, out parsed);
        }

        internal static bool Parse(string version, bool allowTruncation, bool raiseError, out MediaWikiVersion parsed)
        {
            if (string.IsNullOrEmpty(version))
            {
                if (!raiseError) goto SILENT_FAIL;
                throw new ArgumentException(Prompts.ExceptionArgumentNullOrEmpty, nameof(version));
            }

            // 1.0.0-rc-1
            var match = versionRegex.Match(version);
            if (!match.Success)
            {
                if (!raiseError) goto SILENT_FAIL;
                throw new FormatException(Prompts.ExceptionVersionMalformed);
            }

            short major,minor, revision;
            try
            {
                major = match.Groups["Major"].Success ? short.Parse(match.Groups["Major"].Value) : (short)0;
                minor = match.Groups["Minor"].Success ? short.Parse(match.Groups["Minor"].Value) : (short)0;
                revision = match.Groups["Revision"].Success ? short.Parse(match.Groups["Revision"].Value) : (short)0;
            }
            catch (Exception ex)
            {
                if (!raiseError) goto SILENT_FAIL;
                throw new FormatException(Prompts.ExceptionVersionInvalidNumber, ex);
            }

            var devChannel = MediaWikiDevChannel.None;
            short devVersion = 0;
            if (match.Groups["DevChannel"].Success)
            {
                var devChannelExpr = match.Groups["DevChannel"].Value;
                if (string.Equals(devChannelExpr, "wmf", StringComparison.OrdinalIgnoreCase))
                {
                    devChannel = MediaWikiDevChannel.Wmf;
                }
                else if (string.Equals(devChannelExpr, "alpha", StringComparison.OrdinalIgnoreCase))
                {
                    devChannel = MediaWikiDevChannel.Alpha;
                }
                else if (string.Equals(devChannelExpr, "beta", StringComparison.OrdinalIgnoreCase))
                {
                    devChannel = MediaWikiDevChannel.Beta;
                }
                else if (string.Equals(devChannelExpr, "rc", StringComparison.OrdinalIgnoreCase))
                {
                    devChannel = MediaWikiDevChannel.RC;
                }
                else
                {
                    Debug.Fail("Have you forgotten to add MediaWikiDevChannel enum member?");
                }
                try
                {
                    devVersion = match.Groups["DevVersion"].Success ? short.Parse(match.Groups["DevVersion"].Value) : (short)0;
                }
                catch (Exception ex)
                {
                    if (!raiseError) goto SILENT_FAIL;
                    throw new FormatException(Prompts.ExceptionVersionInvalidDevVersion, ex);
                }
            }

            if (!allowTruncation && match.Groups["UnknownSuffix"].Success)
            {
                if (!raiseError) goto SILENT_FAIL;
                throw new FormatException(string.Format(Prompts.ExceptionVersionTruncated1, match.Groups["UnknownSuffix"].Value));
            }

            parsed = new MediaWikiVersion(major, minor, revision, devChannel, devVersion);
            return true;
            SILENT_FAIL:
            parsed = default;
            return false;
        }

        /// <summary>
        /// Initializes a new <seealso cref="MediaWikiVersion"/> instance with major and minor versions.
        /// </summary>
        /// <inheritdoc cref="MediaWikiVersion(short,short,short,MediaWikiDevChannel,short)"/>
        public MediaWikiVersion(short major, short minor)
            : this(major, minor, 0, MediaWikiDevChannel.None, 0)
        {
        }

        /// <summary>
        /// Initializes a new <seealso cref="MediaWikiVersion"/> instance with major, minor, revision versions.
        /// </summary>
        /// <inheritdoc cref="MediaWikiVersion(short,short,short,MediaWikiDevChannel,short)"/>
        public MediaWikiVersion(short major, short minor, short revision)
            : this(major, minor, revision, MediaWikiDevChannel.None, 0)
        {
        }

        /// <summary>
        /// Initializes a new <seealso cref="MediaWikiVersion"/> instance with major, minor, revision versions and
        /// dev-channel.
        /// </summary>
        /// <inheritdoc cref="MediaWikiVersion(short,short,short,MediaWikiDevChannel,short)"/>
        public MediaWikiVersion(short major, short minor, short revision, MediaWikiDevChannel devChannel)
            : this(major, minor, revision, devChannel, 0)
        {
        }

        /// <summary>
        /// Initializes a new <seealso cref="MediaWikiVersion"/> instance with major, minor versions and
        /// dev-version.
        /// </summary>
        /// <inheritdoc cref="MediaWikiVersion(short,short,short,MediaWikiDevChannel,short)"/>
        public MediaWikiVersion(short major, short minor, MediaWikiDevChannel devChannel, short devVersion)
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
        public MediaWikiVersion(short major, short minor, short revision, MediaWikiDevChannel devChannel, short devVersion)
        {
            if (major < 0)
                throw new ArgumentOutOfRangeException(nameof(major));
            if (minor < 0)
                throw new ArgumentOutOfRangeException(nameof(minor));
            if (revision < 0)
                throw new ArgumentOutOfRangeException(nameof(revision));
            _Major = major;
            _Minor = minor;
            _Revision = revision;
            fullDevVersion = MakeFullDevVersion(devChannel, devVersion);
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
        public override int GetHashCode() => HashCode.Combine(_Major, _Minor, _Revision, fullDevVersion);

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
        public override bool Equals(object? obj)
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
        public int CompareTo(object? obj)
        {
            if (obj is null) return 1;
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

        private static ushort MakeFullDevVersion(MediaWikiDevChannel devChannel, short devVersion)
        {
            if (devChannel < MediaWikiDevChannel.None || devChannel > MediaWikiDevChannel.RC)
                throw new ArgumentOutOfRangeException(nameof(devChannel));
            if (devVersion < 0 || devVersion > 0x0FFF)
                throw new ArgumentOutOfRangeException(nameof(devVersion));
            if (devChannel == MediaWikiDevChannel.None && devVersion != 0)
                throw new ArgumentException(Prompts.ExceptionDevVersionRequiresDevChannel, nameof(devVersion));
            return (ushort)(((uint)devChannel << 12) | (ushort)devVersion);
        }

        private static bool IsVersionSegmentInRange(int value, Range range, string rangeParamName)
        {
            var start = range.Start;
            var end = range.End;
            if (start.IsFromEnd)
                throw new ArgumentException("Lower bound of version segment cannot count from end.", rangeParamName);
            if (end.IsFromEnd && end.Value > 0)
                throw new ArgumentException("Upper bound of version segment cannot count more than 0 from end.", rangeParamName);
            return value >= start.Value && (end.IsFromEnd || value < end.Value);
        }

        /// <summary>Determines whether the version is in the specified range.</summary>
        /// <param name="major">major version.</param>
        public bool In(short major)
        {
            return _Major == major;
        }

        /// <summary>Determines whether the version is in the specified range.</summary>
        /// <param name="major">range of major version.</param>
        public bool In(Range major)
        {
            return IsVersionSegmentInRange(_Major, major, nameof(major));
        }

        /// <summary>Determines whether the version is in the specified range.</summary>
        /// <param name="major">major version.</param>
        /// <param name="minor">minor version.</param>
        public bool In(short major, short minor)
        {
            return _Major == major && _Minor == minor;
        }

        /// <summary>Determines whether the version is in the specified range.</summary>
        /// <param name="major">major version.</param>
        /// <param name="minor">range of minor version.</param>
        public bool In(short major, Range minor)
        {
            if (_Major != major) return false;
            return IsVersionSegmentInRange(_Minor, minor, nameof(minor));
        }

        /// <summary>Determines whether the version is in the specified range.</summary>
        /// <param name="major">major version.</param>
        /// <param name="minor">minor version.</param>
        /// <param name="revision">revision number.</param>
        public bool In(short major, short minor, short revision)
        {
            return _Major == major && _Minor == minor && _Revision == revision;
        }

        /// <summary>Determines whether the version is in the specified range.</summary>
        /// <param name="major">major version.</param>
        /// <param name="minor">minor version.</param>
        /// <param name="revision">range of revision number.</param>
        public bool In(short major, short minor, Range revision)
        {
            if (_Major != major) return false;
            if (_Minor != minor) return false;
            return IsVersionSegmentInRange(_Revision, revision, nameof(revision));
        }

        /// <inheritdoc cref="Above(short,short,short)"/>
        public bool Above(short major, short minor)
            => Above(major, minor, 0);

        /// <summary>Determines whether the version is equal to or above than the specified official release version.</summary>
        /// <param name="major">major version.</param>
        /// <param name="minor">minor version.</param>
        /// <param name="revision">revision number.</param>
        public bool Above(short major, short minor, short revision)
        {
            if (_Major != major) return _Major > major;
            if (_Minor != minor) return _Minor > minor;
            if (_Revision != revision) return _Revision > revision;
            return fullDevVersion != 0;
        }

        /// <inheritdoc cref="Above(short,short,short,MediaWikiDevChannel,short)"/>
        public bool Above(short major, short minor, MediaWikiDevChannel devChannel, short devVersion)
            => Above(major, minor, 0, devChannel, devVersion);

        /// <summary>Determines whether the version is equal to or above than the specified version.</summary>
        /// <param name="major">major version.</param>
        /// <param name="minor">minor version.</param>
        /// <param name="revision">revision number.</param>
        /// <param name="devChannel">Channel of development.</param>
        /// <param name="devVersion">DevVersion. Should be between 0 and 4095.</param>
        /// <exception cref="ArgumentOutOfRangeException">Any one of the version numbers is out of range.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="devChannel"/> is <see cref="MediaWikiDevChannel.None"/>,
        /// but <paramref name="devVersion"/> is non-zero.
        /// </exception>
        public bool Above(short major, short minor, short revision, MediaWikiDevChannel devChannel, short devVersion)
        {
            var fdv = MakeFullDevVersion(devChannel, devVersion);
            if (_Major != major) return _Major > major;
            if (_Minor != minor) return _Minor > minor;
            if (_Revision != revision) return _Revision > revision;
            // 0: official release
            if (fdv == 0) return fullDevVersion == 0;
            return fullDevVersion >= fdv;
        }

    }
}
