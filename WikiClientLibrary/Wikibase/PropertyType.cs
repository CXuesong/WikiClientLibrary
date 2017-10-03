using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;

namespace WikiClientLibrary.Wikibase
{
    /// <summary>
    /// Atomic instances indicating a Wikibase Property type.
    /// </summary>
    public abstract class PropertyType
    {
        public abstract string Name { get; }

        public abstract object Parse(JToken expr);

        public abstract JToken ToJson(object value);
    }

    internal class DelegatePropertyType<T> : PropertyType
    {

        private readonly Func<JToken, T> parseHandler;
        private readonly Func<T, JToken> toJsonHandler;

        public DelegatePropertyType(string name, Func<JToken, T> parseHandler, Func<T, JToken> toJsonHandler)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            this.parseHandler = parseHandler ?? throw new ArgumentNullException(nameof(parseHandler));
            this.toJsonHandler = toJsonHandler ?? throw new ArgumentNullException(nameof(toJsonHandler));
        }

        public override string Name { get; }

        public override object Parse(JToken expr)
        {
            return parseHandler(expr);
        }

        public override JToken ToJson(object value)
        {
            if (value == null) return null;
            if (value is T t)
                return toJsonHandler(t);
            throw new ArgumentException("Value type is incompatible.", nameof(value));
        }

    }

    public static class PropertyTypes
    {

        public static PropertyType WikibaseItem { get; } = new DelegatePropertyType<string>("wikibase-item",
            e => (string) e, v => v);

        public static PropertyType WikibaseProperty { get; } = new DelegatePropertyType<string>("wikibase-property",
            e => (string)e, v => v);

        public static PropertyType String { get; } = new DelegatePropertyType<string>("string",
            e => (string) e, v => v);

        public static PropertyType CommonsMedia { get; } = new DelegatePropertyType<string>("commonsMedia",
            e => (string) e, v => v);

        public static PropertyType Url { get; } = new DelegatePropertyType<string>("url",
            e => (string) e, v => v);

        public static PropertyType Time { get; } = new DelegatePropertyType<WikibaseTime>("time",
            e =>
            {
                var time = (string) e["time"];
                var timeZone = (int) e["timezone"];
                var before = (int) e["before"];
                var after = (int) e["after"];
                var precision = (WikibaseTimePrecision) (int) e["precision"];
                var calendar = (string) e["calendarmodel"];
                return new WikibaseTime(time, before, after, timeZone, precision, calendar);
            }, v =>
            {
                var obj = new JObject
                {
                    {"time", v.ToIso8601UtcString()},
                    {"timezone", v.TimeZone},
                    {"before", v.Before},
                    {"after", v.After},
                    {"precision", (int) v.Precision},
                    {"calendarmodel", v.CalendarModel.Uri}
                };
                return obj;
            });

        // No scientific notation. It's desirable.
        private const string SignedFloatFormat = "+0.#################;-0.#################;0";

        public static PropertyType Amount { get; } = new DelegatePropertyType<WikibaseAmount>("amount",
            e =>
            {
                var amount = Convert.ToDouble((string) e["amount"]);
                var unit = (string) e["unit"];
                var lb = (string) e["lowerBound"];
                var ub = (string) e["upperBound"];
                return new WikibaseAmount(amount,
                    lb == null ? amount : Convert.ToDouble(lb),
                    ub == null ? amount : Convert.ToDouble(ub),
                    unit);
            }, v =>
            {
                var obj = new JObject
                {
                    {"amount", v.Amount.ToString(SignedFloatFormat)},
                    {"unit", v.Unit.Uri},
                };
                if (v.HasError)
                {
                    obj.Add("lowerBound", v.LowerBound.ToString(SignedFloatFormat));
                    obj.Add("upperBound", v.UpperBound.ToString(SignedFloatFormat));
                }
                return obj;
            });

        public static PropertyType MonolingualText { get; } = new DelegatePropertyType<WikibaseMonolingualText>(
            "monolingualtext",
            e => new WikibaseMonolingualText((string) e["text"], (string) e["language"]),
            v => new JObject {{"text", v.Text}, {"language", v.Language}});

        public static PropertyType Math { get; } = new DelegatePropertyType<string>("math",
            e => (string)e, v => v);

        /// <summary>
        /// Literal data field for an external identifier.
        /// External identifiers may automatically be linked to an authoritative resource for display.
        /// </summary>
        public static PropertyType ExternalId { get; } = new DelegatePropertyType<string>("external-id",
            e => (string)e, v => v);

        public static PropertyType GlobeCoordinate { get; } = new DelegatePropertyType<WikibaseGlobeCoordinate>(
            "globe-coordinate",
            e => new WikibaseGlobeCoordinate((double) e["latitude"], (double) e["longitude"],
                (double) e["precision"], (string) e["globe"]),
            v => new JObject
            {
                {"latitude", v.Latitude}, {"longitude", v.Longitude},
                {"precision", v.Precision}, {"globe", v.Globe.Uri},
            });

        /// <summary>
        /// Link to geographic map data stored on Wikimedia Commons (or other configured wiki).
        /// See "https://www.mediawiki.org/wiki/Help:Map_Data" for more documentation about map data.
        /// </summary>
        public static PropertyType GeoShape { get; } = new DelegatePropertyType<string>("geo-shape",
            e => (string)e, v => v);

        /// <summary>
        /// Link to tabular data stored on Wikimedia Commons (or other configured wiki).
        /// See "https://www.mediawiki.org/wiki/Help:Tabular_Data" for more documentation about tabular data.
        /// </summary>
        public static PropertyType TabularData { get; } = new DelegatePropertyType<string>("tabular-data",
            e => (string) e, v => v);

        private static readonly Dictionary<string, PropertyType> typeDict = new Dictionary<string, PropertyType>();

        static PropertyTypes()
        {
            foreach (var p in typeof(PropertyTypes).GetRuntimeProperties()
                .Where(p => p.PropertyType == typeof(PropertyType)))
            {
                var value = (PropertyType) p.GetValue(null);
                typeDict.Add(value.Name, value);
            }
        }

        /// <summary>
        /// Tries to get a property type by its name.
        /// </summary>
        /// <param name="typeName">Internal name of the desired property type.</param>
        /// <exception cref="ArgumentNullException"><paramref name="typeName"/> is <c>null</c>.</exception>
        /// <returns>The matching type, or <c>null</c> if cannot find one.</returns>
        public static PropertyType Get(string typeName)
        {
            if (typeName == null) throw new ArgumentNullException(nameof(typeName));
            if (typeDict.TryGetValue(typeName, out var t)) return t;
            return null;
        }

    }
}
