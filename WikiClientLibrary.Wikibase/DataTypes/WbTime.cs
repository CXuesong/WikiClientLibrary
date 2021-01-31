using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace WikiClientLibrary.Wikibase.DataTypes
{

    /// <summary>
    /// The date-time point value used in Wikibase.
    /// </summary>
    public struct WbTime : IEquatable<WbTime>
    {

        /// <summary>
        /// The URI for Gregorian calendar.
        /// </summary>
        /// <remarks>The URI value is <a herf="http://www.wikidata.org/entity/Q1985727">http://www.wikidata.org/entity/Q1985727</a>.</remarks>
        public static Uri GregorianCalendar { get; } = new Uri("http://www.wikidata.org/entity/Q1985727");

        /// <summary>
        /// The URI for Julian calendar.
        /// </summary>
        /// <remarks>The URI value is <a herf="http://www.wikidata.org/entity/Q1985786">http://www.wikidata.org/entity/Q1985786</a>.</remarks>
        public static Uri JulianCalendar { get; } = new Uri("http://www.wikidata.org/entity/Q1985786");

        /// <summary>Initialize a new instance of <see cref="WbTime"/> with the specified time point, time zone,
        /// precision and calendar model.</summary>
        /// <param name="year">The year (-999999999 through 999999999).</param>
        /// <param name="month">The month (positive integer).</param>
        /// <param name="day">The day (positive integer).</param>
        /// <param name="hour">The hour (0 through 23).</param>
        /// <param name="minute">The hour (0 through 59).</param>
        /// <param name="second">The hour (0 through 59).</param>
        /// <param name="before">Number of units before the given time it could be, if uncertain. The unit is given by <paramref name="precision"/>.</param>
        /// <param name="after">Number of units after the given time it could be, if uncertain. The unit is given by <paramref name="precision"/>.</param>
        /// <param name="timeZone">Time zone offset in minutes. See <see cref="TimeZone"/> for more information.</param>
        /// <param name="precision">The unit of the precision of the time.</param>
        /// <param name="calendarModel">The entity URI of the calendar model.</param>
        /// <exception cref="ArgumentNullException"><paramref name="calendarModel"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">One or more numeric parameters are out of range.</exception>
        public WbTime(int year, int month, int day, int hour, int minute, int second,
            int before, int after, int timeZone,
            WikibaseTimePrecision precision, Uri calendarModel)
        {
            if (calendarModel == null) throw new ArgumentNullException(nameof(calendarModel));
            if (precision < WikibaseTimePrecision.YearE9 || precision > WikibaseTimePrecision.Second)
                throw new ArgumentOutOfRangeException(nameof(precision));
            if (precision >= WikibaseTimePrecision.Month && month <= 0)
                throw new ArgumentOutOfRangeException(nameof(month));
            if (precision >= WikibaseTimePrecision.Day && day <= 0)
                throw new ArgumentOutOfRangeException(nameof(day));
            if (precision >= WikibaseTimePrecision.Hour && (hour < 0 || hour >= 24))
                throw new ArgumentOutOfRangeException(nameof(hour));
            if (precision >= WikibaseTimePrecision.Minute && (minute < 0 || minute >= 60))
                throw new ArgumentOutOfRangeException(nameof(minute));
            if (precision >= WikibaseTimePrecision.Second && (second < 0 || second >= 60))
                throw new ArgumentOutOfRangeException(nameof(second));
            Year = year;
            Month = month;
            Day = day;
            Hour = hour;
            Minute = minute;
            Second = second;
            Before = before;
            After = after;
            TimeZone = timeZone;
            Precision = precision;
            CalendarModel = calendarModel;
        }

        // +2007-06-07T00:00:00Z
        private static readonly Regex ISO8601Matcher =
            new Regex(@"^\s*(?<Y>[\+-]?\d{1,9})-(?<M>\d\d?)-(?<D>\d\d?)T(?<H>\d\d?):(?<m>\d\d?):(?<S>\d\d?)(?<K>Z|[\+-]\d\d?:\d\d?)?\s*$");

        private static Uri GetCalendarModel(int year, int month)
        {
            if (year > 1582) return GregorianCalendar;
            if (year == 1582 && month >= 10) return GregorianCalendar;
            return JulianCalendar;
        }

        /// <summary>Constructs a <see cref="WbTime"/> instance from <see cref="DateTime"/>, using the appropriate calendar model.</summary>
        /// <inheritdoc cref="FromDateTime(DateTime,int,int,WikibaseTimePrecision,Uri)"/>
        /// <remarks>This overload uses 0 as <see cref="TimeZone"/> if <see cref="DateTime.Kind"/>
        /// is <see cref="DateTimeKind.Utc"/>, and uses local time zone otherwise.</remarks>
        public static WbTime FromDateTime(DateTime dateTime, WikibaseTimePrecision precision)
        {
            return FromDateTime(dateTime, 0, 0, precision, GetCalendarModel(dateTime.Year, dateTime.Month));
        }

        /// <inheritdoc cref="FromDateTime(DateTime,int,int,WikibaseTimePrecision,Uri)"/>
        /// <remarks>This overload uses 0 as <see cref="TimeZone"/> if <see cref="DateTime.Kind"/>
        /// is <see cref="DateTimeKind.Utc"/>, and uses local time zone otherwise.</remarks>
        public static WbTime FromDateTime(DateTime dateTime, int before, int after, WikibaseTimePrecision precision)
        {
            return FromDateTime(dateTime, before, after, precision, null);
        }
        /// <summary>Constructs a <see cref="WbTime"/> instance from <see cref="DateTime"/>.</summary>
        /// <inheritdoc cref="WbTime(int,int,int,int,int,int,int,int,int,WikibaseTimePrecision,Uri)"/>
        /// <remarks>This overload uses 0 as <see cref="TimeZone"/> if <see cref="DateTime.Kind"/>
        /// is <see cref="DateTimeKind.Utc"/>, and uses local time zone otherwise.</remarks>
        public static WbTime FromDateTime(DateTime dateTime, int before, int after, WikibaseTimePrecision precision,
            Uri calendarModel)
        {
            if (calendarModel == null) throw new ArgumentNullException(nameof(calendarModel));
            var timeZone = dateTime.Kind == DateTimeKind.Utc ? 0 : (int)TimeZoneInfo.Local.BaseUtcOffset.TotalMinutes;
            return new WbTime(dateTime.Year, dateTime.Month, dateTime.Day,
                dateTime.Hour, dateTime.Minute, dateTime.Minute,
                before, after, timeZone,
                precision, calendarModel);
        }

        /// <inheritdoc cref="FromDateTimeOffset(DateTimeOffset,int,int,WikibaseTimePrecision,Uri)"/>
        /// <summary>Constructs a <see cref="WbTime"/> instance from <see cref="DateTimeOffset"/>, using the appropriate calendar model.</summary>
        /// <param name="dateTime">The date, time, and time zone.</param>
        public static WbTime FromDateTimeOffset(DateTimeOffset dateTime, int before, int after,
            WikibaseTimePrecision precision)
        {
            return FromDateTimeOffset(dateTime, before, after, precision, GetCalendarModel(dateTime.Year, dateTime.Month));
        }

        /// <inheritdoc cref="WbTime(int,int,int,int,int,int,int,int,int,WikibaseTimePrecision,Uri)"/>
        /// <summary>Constructs a <see cref="WbTime"/> instance from <see cref="DateTimeOffset"/>.</summary>
        /// <param name="dateTime">The date, time, and time zone.</param>
        public static WbTime FromDateTimeOffset(DateTimeOffset dateTime, int before, int after,
            WikibaseTimePrecision precision, Uri calendarModel)
        {
            return new WbTime(dateTime.Year, dateTime.Month, dateTime.Day,
                dateTime.Hour, dateTime.Minute, dateTime.Minute,
                before, after, (int)dateTime.Offset.TotalMinutes,
                precision, calendarModel);
        }

        /// <summary>Parses <see cref="WbTime"/> from its string representation.</summary>
        /// <inheritdoc cref="WbTime(int,int,int,int,int,int,int,int,int,WikibaseTimePrecision,Uri)"/>
        /// <param name="dateTime">The string representation of the date and time. Currently only ISO-8601 format is supported.</param>
        public static WbTime Parse(string dateTime, int before, int after, int timeZone,
            WikibaseTimePrecision precision, Uri calendarModel)
        {
            if (dateTime == null) throw new ArgumentNullException(nameof(dateTime));
            if (calendarModel == null) throw new ArgumentNullException(nameof(calendarModel));
            var dateTimeMatch = ISO8601Matcher.Match(dateTime);
            if (!dateTimeMatch.Success) throw new ArgumentException("Invalid ISO-8601 time format.", nameof(dateTime));
            var year = Convert.ToInt32(dateTimeMatch.Groups["Y"].Value);
            var month = Convert.ToInt32(dateTimeMatch.Groups["M"].Value);
            var day = Convert.ToInt32(dateTimeMatch.Groups["D"].Value);
            var hour = Convert.ToInt32(dateTimeMatch.Groups["H"].Value);
            var minute = Convert.ToInt32(dateTimeMatch.Groups["m"].Value);
            var second = Convert.ToInt32(dateTimeMatch.Groups["S"].Value);
            return new WbTime(year, month, day, hour, minute, second, before, after, timeZone, precision, calendarModel);
        }

        public int Year { get; }

        public int Month { get; }

        public int Day { get; }

        public int Hour { get; }

        public int Minute { get; }

        public int Second { get; }

        /// <summary>
        /// Number of units after the given time it could be, if uncertain. The unit is given by the precision.
        /// </summary>
        public int Before { get; }

        /// <summary>
        /// Number of units before the given time it could be, if uncertain. The unit is given by the precision.
        /// </summary>
        public int After { get; }

        /// <summary>
        /// Time zone offset in minutes.
        /// </summary>
        /// <remarks>
        /// <para>Timezone information is given in three different ways depending on the time:</para>
        /// <list type="bullet">
        /// <item><description>Times after the implementation of UTC (1972): as an offset from UTC in minutes;</description></item>
        /// <item><description>Times before the implementation of UTC: the offset of the time zone from universal time;</description></item>
        /// <item><description>Before the implementation of time zones: The longitude of the place of
        /// the event, in the range −180° to 180°, multiplied by 4 to convert to minutes.
        /// </description></item>
        /// </list>
        /// </remarks>
        public int TimeZone { get; }

        /// <summary>
        /// The unit of the precision of the time.
        /// </summary>
        public WikibaseTimePrecision Precision { get; }

        /// <summary>
        /// The entity URI of the calendar model.
        /// </summary>
        public Uri CalendarModel { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            return Precision switch
            {
                WikibaseTimePrecision.Month => $"{Year:0000}-{Month:00}",
                WikibaseTimePrecision.Day => $"{Year:0000}-{Month:00}-{Day:00}",
                WikibaseTimePrecision.Hour => $"{Year:0000}-{Month:00}-{Day:00} {Hour:00}h",
                WikibaseTimePrecision.Minute => $"{Year:0000}-{Month:00}-{Day:00} {Hour:00}:{Minute:00}",
                WikibaseTimePrecision.Second => $"{Year:0000}-{Month:00}-{Day:00} {Hour:00}:{Minute:00}:{Second:00}",
                _ => "Y" + Year
            };
        }

        /// <summary>
        /// Formats the date and time part into ISO-8601 UTC date time format.
        /// </summary>
        public string ToIso8601UtcString()
        {
            return $"{Year:+0000;-0000;0000}-{Month:00}-{Day:00}T{Hour:00}:{Minute:00}:{Second:00}Z";
        }

        /// <summary>
        /// Converts to <see cref="DateTimeOffset"/>, using appropriate rounding specified by <see cref="Precision"/>.
        /// </summary>
        public DateTimeOffset ToDateTimeOffset()
        {
            if (Year < 1 || Year > 9999)
                throw new OverflowException("The year is too large or small to be represented in DateTimeOffset.");
            switch (Precision)
            {
                case WikibaseTimePrecision.Millenia:
                    return new DateTimeOffset(Year / 1000 * 1000, 1, 1, 0, 0, 0, TimeSpan.FromMinutes(TimeZone));
                case WikibaseTimePrecision.Century:
                    return new DateTimeOffset(Year / 100 * 100, 1, 1, 0, 0, 0, TimeSpan.FromMinutes(TimeZone));
                case WikibaseTimePrecision.Decade:
                    return new DateTimeOffset(Year / 10 * 10, 1, 1, 0, 0, 0, TimeSpan.FromMinutes(TimeZone));
                case WikibaseTimePrecision.Year:
                    return new DateTimeOffset(Year, 1, 1, 0, 0, 0, TimeSpan.FromMinutes(TimeZone));
                case WikibaseTimePrecision.Month:
                    return new DateTimeOffset(Year, Month, 1, 0, 0, 0, TimeSpan.FromMinutes(TimeZone));
                case WikibaseTimePrecision.Day:
                    return new DateTimeOffset(Year, Month, Day, 0, 0, 0, TimeSpan.FromMinutes(TimeZone));
                case WikibaseTimePrecision.Hour:
                    return new DateTimeOffset(Year, Month, Day, Hour, 0, 0, TimeSpan.FromMinutes(TimeZone));
                case WikibaseTimePrecision.Minute:
                    return new DateTimeOffset(Year, Month, Day, Hour, Minute, 0, TimeSpan.FromMinutes(TimeZone));
                case WikibaseTimePrecision.Second:
                    return new DateTimeOffset(Year, Month, Day, Hour, Minute, Second, TimeSpan.FromMinutes(TimeZone));
                default:
                    // Year, significant digit too large
                    Debug.Assert(false);
                    return new DateTimeOffset(1, 1, 1, 0, 0, 0, TimeSpan.FromMinutes(TimeZone));
            }
        }

        /// <inheritdoc />
        public bool Equals(WbTime other)
        {
            return Year == other.Year && Month == other.Month && Day == other.Day && Hour == other.Hour && Minute == other.Minute && Second == other.Second &&
                   Before == other.Before && After == other.After && TimeZone == other.TimeZone && Precision == other.Precision &&
                   string.Equals(CalendarModel, other.CalendarModel);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is WbTime time && Equals(time);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Year);
            hash.Add(Month);
            hash.Add(Day);
            hash.Add(Hour);
            hash.Add(Minute);
            hash.Add(Second);
            hash.Add(Before);
            hash.Add(After);
            hash.Add(TimeZone);
            hash.Add(Precision);
            hash.Add(CalendarModel);
            return hash.ToHashCode();
        }

        public static bool operator ==(WbTime left, WbTime right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WbTime left, WbTime right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// The unit of precision of the time.
    /// </summary>
    public enum WikibaseTimePrecision
    {
        /// <summary>1,000,000,000 years.</summary>
        YearE9 = 0,
        /// <summary>100,000,000 years.</summary>
        YearE8 = 1,
        /// <summary>10,000,000 years.</summary>
        YearE7 = 2,
        /// <summary>1,000,000 years.</summary>
        YearE6 = 3,
        /// <summary>100,000 years.</summary>
        YearE5 = 4,
        /// <summary>10,000 years.</summary>
        YearE4 = 5,
        /// <summary>1,000 years.</summary>
        Millenia = 6,
        /// <summary>100 years.</summary>
        Century = 7,
        /// <summary>10 years.</summary>
        Decade = 8,
        /// <summary>Years.</summary>
        Year = 9,
        /// <summary>Months.</summary>
        Month = 10,
        /// <summary>Days.</summary>
        Day = 11,
        /// <summary>Hours.</summary>
        Hour = 12,
        /// <summary>Minutes.</summary>
        Minute = 13,
        /// <summary>Seconds.</summary>
        Second = 14
    }

}
