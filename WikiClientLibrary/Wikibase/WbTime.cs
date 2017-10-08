using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace WikiClientLibrary.Wikibase
{

    /// <summary>
    /// The date-time point value used in Wikibase.
    /// </summary>
    public struct WbTime : IEquatable<WbTime>
    {

        public WbTime(int year, int month, int day, int hour, int minute, int second,
            int before, int after, int timeZone,
            WikibaseTimePrecision precision, WbUri calendarModel)
        {
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
            CalendarModel = calendarModel ?? throw new ArgumentNullException(nameof(calendarModel));
        }

        // +2007-06-07T00:00:00Z
        private static readonly Regex ISO8601Matcher =
            new Regex(@"^\s*(?<Y>[\+-]?\d{1,9})-(?<M>\d\d?)-(?<D>\d\d?)T(?<H>\d\d?):(?<m>\d\d?):(?<S>\d\d?)(?<K>Z|[\+-]\d\d?:\d\d?)?\s*$");

        public WbTime(string dateTime, int before, int after, int timeZone,
            WikibaseTimePrecision precision, WbUri calendarModel)
        {
            if (dateTime == null) throw new ArgumentNullException(nameof(dateTime));
            var dateTimeMatch = ISO8601Matcher.Match(dateTime);
            if (!dateTimeMatch.Success) throw new ArgumentException("Invalid ISO-8601 time format.", nameof(dateTime));
            var year = Convert.ToInt32(dateTimeMatch.Groups["Y"].Value);
            var month = Convert.ToInt32(dateTimeMatch.Groups["M"].Value);
            var day = Convert.ToInt32(dateTimeMatch.Groups["D"].Value);
            var hour = Convert.ToInt32(dateTimeMatch.Groups["H"].Value);
            var minute = Convert.ToInt32(dateTimeMatch.Groups["m"].Value);
            var second = Convert.ToInt32(dateTimeMatch.Groups["S"].Value);
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
            CalendarModel = calendarModel ?? throw new ArgumentNullException(nameof(calendarModel));
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
        /// URI identifying the calendar model.
        /// </summary>
        public WbUri CalendarModel { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            switch (Precision)
            {
                case WikibaseTimePrecision.Month:
                    return $"{Year:0000}-{Month:00}";
                case WikibaseTimePrecision.Day:
                    return $"{Year:0000}-{Month:00}-{Day:00}";
                case WikibaseTimePrecision.Hour:
                    return $"{Year:0000}-{Month:00}-{Day:00} {Hour:00}h";
                case WikibaseTimePrecision.Minute:
                    return $"{Year:0000}-{Month:00}-{Day:00} {Hour:00}:{Minute:00}";
                case WikibaseTimePrecision.Second:
                    return $"{Year:0000}-{Month:00}-{Day:00} {Hour:00}:{Minute:00}:{Second:00}";
                default:
                    return "Y" + Year;
            }
        }

        public string ToIso8601UtcString()
        {
            return $"{Year:+0000;-0000;0000}-{Month:00}-{Day:00}T{Hour:00}:{Minute:00}:{Second:00}Z";
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
            unchecked
            {
                var hashCode = Year;
                hashCode = (hashCode * 397) ^ Month;
                hashCode = (hashCode * 397) ^ Day;
                hashCode = (hashCode * 397) ^ Hour;
                hashCode = (hashCode * 397) ^ Minute;
                hashCode = (hashCode * 397) ^ Second;
                hashCode = (hashCode * 397) ^ Before;
                hashCode = (hashCode * 397) ^ After;
                hashCode = (hashCode * 397) ^ TimeZone;
                hashCode = (hashCode * 397) ^ (int) Precision;
                hashCode = (hashCode * 397) ^ CalendarModel.GetHashCode();
                return hashCode;
            }
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
