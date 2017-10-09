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
    public abstract class WbPropertyType
    {
        public abstract string Name { get; }

        public abstract string ValueTypeName { get; }

        public abstract object Parse(JToken expr);

        public abstract JToken ToJson(object value);

        /// <inheritdoc />
        public override string ToString()
        {
            var name = Name;
            var vtName = ValueTypeName;
            if (name == vtName) return name;
            return name + "(" + vtName + ")";
        }
    }

    internal sealed class DelegatePropertyType<T> : WbPropertyType
    {

        private readonly Func<JToken, T> parseHandler;
        private readonly Func<T, JToken> toJsonHandler;

        public DelegatePropertyType(string name, Func<JToken, T> parseHandler, Func<T, JToken> toJsonHandler) 
            : this(name, name, parseHandler, toJsonHandler)
        {
        }

        public DelegatePropertyType(string name, string valueTypeName, Func<JToken, T> parseHandler, Func<T, JToken> toJsonHandler)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ValueTypeName = valueTypeName ?? throw new ArgumentNullException(nameof(valueTypeName));
            this.parseHandler = parseHandler ?? throw new ArgumentNullException(nameof(parseHandler));
            this.toJsonHandler = toJsonHandler ?? throw new ArgumentNullException(nameof(toJsonHandler));
        }

        /// <inheritdoc />
        public override string Name { get; }

        /// <inheritdoc />
        public override string ValueTypeName { get; }

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

    internal sealed class MissingPropertyType : WbPropertyType
    {
        private MissingPropertyType(string name, string valueTypeName)
        {
            Name = name;
            ValueTypeName = valueTypeName;
        }

        public static MissingPropertyType Get(string name, string valueTypeName)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            // TODO Make atomic.
            return new MissingPropertyType(name, valueTypeName ?? name);
        }

        /// <inheritdoc />
        public override string Name { get; }

        /// <inheritdoc />
        public override string ValueTypeName { get; }

        /// <inheritdoc />
        public override object Parse(JToken expr)
        {
            throw new NotSupportedException("Property type " + this + " is not supported.");
        }

        /// <inheritdoc />
        public override JToken ToJson(object value)
        {
            throw new NotSupportedException("Property type " + this + " is not supported.");
        }
    }

    public static class WbPropertyTypes
    {

        private static string EntityIdFromJson(JToken value)
        {
            var id = (string) value["id"];
            id = null;
            if (id != null) return id;
            var type = (string) value["entity-type"];
            switch (type)
            {
                case "item":
                    id = "Q";
                    break;
                case "property":
                    id = "P";
                    break;
                default:
                    throw new ArgumentException("Invalid entity-type: " + type + ".", nameof(value));
            }
            id += (string) value["numeric-id"];
            return id;
        }

        private static JToken EntityIdToJson(string id)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            id = id.Trim();
            if (id.Length < 2) throw new ArgumentException("Invalid entity identifier.", nameof(id));
                int idValue;
            try
            {
                idValue = Convert.ToInt32(id.Substring(1));
            }
            catch (FormatException)
            {
                throw new ArgumentException("Invalid entity identifier. Expect numeric id follows.", nameof(id));
            }
            var value = new JObject();
            switch (id[0])
            {
                case 'P':
                case 'p':
                    value.Add("entity-type", "property");
                    break;
                case 'Q':
                case 'q':
                    value.Add("entity-type", "item");
                    break;
                default:
                    throw new ArgumentException("Unknown entity prefix: " + id[0] + ".", nameof(id));
            }
            value.Add("numeric-id", idValue);
            return value;
        }

        public static WbPropertyType WikibaseItem { get; }
            = new DelegatePropertyType<string>("wikibase-item", "wikibase-entityid",
                EntityIdFromJson, EntityIdToJson);

        public static WbPropertyType WikibaseProperty { get; }
            = new DelegatePropertyType<string>("wikibase-property", "wikibase-entityid",
                EntityIdFromJson, EntityIdToJson);

        public static WbPropertyType String { get; } = new DelegatePropertyType<string>("string",
            e => (string) e, v => v);

        public static WbPropertyType CommonsMedia { get; }
            = new DelegatePropertyType<string>("commonsMedia", "string",
                e => (string) e, v => v);

        public static WbPropertyType Url { get; } = new DelegatePropertyType<string>("url", "string",
            e => (string) e, v => v);

        public static WbPropertyType Time { get; } = new DelegatePropertyType<WbTime>("time",
            e =>
            {
                var time = (string) e["time"];
                var timeZone = (int) e["timezone"];
                var before = (int) e["before"];
                var after = (int) e["after"];
                var precision = (WikibaseTimePrecision) (int) e["precision"];
                var calendar = (string) e["calendarmodel"];
                return WbTime.Parse(time, before, after, timeZone, precision, calendar);
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

        public static WbPropertyType Quantity { get; } = new DelegatePropertyType<WbQuantity>("quantity",
            e =>
            {
                var amount = Convert.ToDouble((string) e["amount"]);
                var unit = (string) e["unit"];
                var lb = (string) e["lowerBound"];
                var ub = (string) e["upperBound"];
                return new WbQuantity(amount,
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

        public static WbPropertyType MonolingualText { get; }
            = new DelegatePropertyType<WbMonolingualText>("monolingualtext",
                e => new WbMonolingualText((string) e["language"], (string) e["text"]),
                v => new JObject {{"text", v.Text}, {"language", v.Language}});

        public static WbPropertyType Math { get; } 
            = new DelegatePropertyType<string>("math", "string",
            e => (string) e, v => v);

        /// <summary>
        /// Literal data field for an external identifier.
        /// External identifiers may automatically be linked to an authoritative resource for display.
        /// </summary>
        public static WbPropertyType ExternalId { get; }
            = new DelegatePropertyType<string>("external-id", "string",
                e => (string) e, v => v);

        public static WbPropertyType GlobeCoordinate { get; }
            = new DelegatePropertyType<WbGlobeCoordinate>("globe-coordinate", "globecoordinate",
                e => new WbGlobeCoordinate((double) e["latitude"], (double) e["longitude"],
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
        public static WbPropertyType GeoShape { get; } = new DelegatePropertyType<string>("geo-shape", "string",
            e => (string) e, v => v);

        /// <summary>
        /// Link to tabular data stored on Wikimedia Commons (or other configured wiki).
        /// See "https://www.mediawiki.org/wiki/Help:Tabular_Data" for more documentation about tabular data.
        /// </summary>
        public static WbPropertyType TabularData { get; } = new DelegatePropertyType<string>("tabular-data", "string",
            e => (string) e, v => v);

        private static readonly Dictionary<string, WbPropertyType> typeDict = new Dictionary<string, WbPropertyType>();

        static WbPropertyTypes()
        {
            foreach (var p in typeof(WbPropertyTypes).GetRuntimeProperties()
                .Where(p => p.PropertyType == typeof(WbPropertyType)))
            {
                var value = (WbPropertyType) p.GetValue(null);
                typeDict.Add(value.Name, value);
            }
        }

        /// <summary>
        /// Tries to get a property type by its name.
        /// </summary>
        /// <param name="typeName">Internal name of the desired property type.</param>
        /// <exception cref="ArgumentNullException"><paramref name="typeName"/> is <c>null</c>.</exception>
        /// <returns>The matching type, or <c>null</c> if cannot find one.</returns>
        public static WbPropertyType Get(string typeName)
        {
            if (typeName == null) throw new ArgumentNullException(nameof(typeName));
            if (typeDict.TryGetValue(typeName, out var t)) return t;
            return null;
        }

    }
}
