using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;

namespace WikiClientLibrary.Wikibase.DataTypes
{
    /// <summary>
    /// Atomic instances indicating a Wikibase Property type.
    /// </summary>
    /// <remarks>
    /// <para>In Wikibase, there are two different notion of types: one is "property type",
    /// the other is "value type". They follow n:1 relation, i.e. one property type corresponds to
    /// one value type, but the same value type can represent different property types.</para>
    /// <para>For example, <c>wikibase-item</c> property type corresponds to <c>wikibase-entityid</c> value type,
    /// while <c>wikibase-property</c> property type also corresponds to <c>wikibase-entityid</c> value type.
    /// For a list of Wikibase built-in data types, see
    /// <a href="https://www.wikidata.org/wiki/Special:ListDatatypes">d:Special:ListDatatypes</a>.</para>
    /// <para>
    /// For a list of predefined <see cref="WikibaseDataType"/> instances, see <see cref="BuiltInDataTypes"/>.
    /// </para>
    /// </remarks>
    /// <seealso cref="BuiltInDataTypes"/>
    public abstract class WikibaseDataType
    {

        /// <summary>
        /// The property type name.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// The value type name.
        /// </summary>
        public abstract string ValueTypeName { get; }

        /// <summary>
        /// The mapped CLR type for the data type.
        /// </summary>
        public virtual Type MappedType => typeof(object);

        /// <summary>
        /// Converts the JSON to the CLR value.
        /// </summary>
        public abstract object Parse(JToken expr);


        /// <summary>
        /// Converts the CLR value to JSON.
        /// </summary>
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

    internal sealed class DelegatePropertyType<T> : WikibaseDataType
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

        /// <inheritdoc />
        public override Type MappedType => typeof(T);

        public override object Parse(JToken expr)
        {
            return parseHandler(expr);
        }

        public override JToken ToJson(object value)
        {
            if (value == null) return null;
            if (value is T t)
                return toJsonHandler(t);
            throw new ArgumentException($"Value type is incompatible for {Name}: expected {typeof(T)}, received {value.GetType()}.", nameof(value));
        }

    }

    internal sealed class MissingPropertyType : WikibaseDataType
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
        public override Type MappedType => typeof(JToken);

        /// <inheritdoc />
        public override object Parse(JToken expr)
        {
            return expr;
        }

        /// <inheritdoc />
        public override JToken ToJson(object value)
        {
            return JToken.FromObject(value);
        }
    }

    /// <summary>
    /// Provides a list of predefined <see cref="WikibaseDataType"/> instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For a list of Wikibase built-in data types, see the data type list:
    /// <a href="https://www.wikidata.org/wiki/Special:ListDatatypes">d:Special:ListDatatypes</a>.
    /// The description of the static properties in this class are mostly copied from the Wikidata data type list.
    /// </para>
    /// </remarks>
    /// <seealso cref="WikibaseDataType"/>
    public static class BuiltInDataTypes
    {

        private static string EntityIdFromJson(JToken value)
        {
            var id = (string)value["id"];
            if (id != null) return id;
            var type = (string)value["entity-type"];
            id = type switch
            {
                "item" => "Q",
                "property" => "P",
                _ => throw new ArgumentException("Invalid entity-type: " + type + ".", nameof(value))
            };
            id += (string)value["numeric-id"];
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
                idValue = Convert.ToInt32(id[1..]);
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

        /// <summary>
        /// Link to other Items on the project. When a value is entered, the project's "Item" namespace will be searched for matching Items.
        /// </summary>
        /// <remarks>
        /// The value type for this data type is `wikibase-entityid`.
        /// </remarks>
        public static WikibaseDataType WikibaseItem { get; }
            = new DelegatePropertyType<string>("wikibase-item", "wikibase-entityid",
                EntityIdFromJson, EntityIdToJson);

        /// <summary>
        /// Link to Properties on the project. When a value is entered, the project's "Property" namespace will be searched for matching Properties.
        /// </summary>
        /// <remarks>
        /// The value type for this data type is `wikibase-entityid`.
        /// </remarks>
        public static WikibaseDataType WikibaseProperty { get; }
            = new DelegatePropertyType<string>("wikibase-property", "wikibase-entityid",
                EntityIdFromJson, EntityIdToJson);

        public static WikibaseDataType String { get; } = new DelegatePropertyType<string>("string",
            e => (string)e, v => v);

        /// <summary>
        /// Link to files stored at Wikimedia Commons. When a value is entered, the "File" namespace on Commons will be searched for matching files.
        /// </summary>
        /// <remarks>
        /// The value type for this data type is `string`.
        /// </remarks>
        public static WikibaseDataType CommonsMedia { get; }
            = new DelegatePropertyType<string>("commonsMedia", "string",
                e => (string)e, v => v);

        /// <summary>
        /// Literal data field for a URL. URLs are restricted to the protocols also supported for external links in wikitext.
        /// </summary>
        /// <remarks>
        /// The value type for this data type is `string`.
        /// </remarks>
        public static WikibaseDataType Url { get; } = new DelegatePropertyType<string>("url", "string",
            e => (string)e, v => v);

        /// <summary>
        /// Literal data field for a point in time. Given as a date and time with some precision and boundaries.
        /// The time is saved internally in the specified calendar model.
        /// </summary>
        /// <remarks>
        /// The value type for this data type is `time`.
        /// </remarks>
        public static WikibaseDataType Time { get; } = new DelegatePropertyType<WbTime>("time",
            e =>
            {
                var time = (string)e["time"];
                var timeZone = (int)e["timezone"];
                var before = (int)e["before"];
                var after = (int)e["after"];
                var precision = (WikibaseTimePrecision)(int)e["precision"];
                var calendar = (string)e["calendarmodel"];
                return WbTime.Parse(time, before, after, timeZone, precision, WikibaseUriFactory.Get(calendar));
            }, v =>
            {
                var obj = new JObject
                {
                    {"time", v.ToIso8601UtcString()},
                    {"timezone", v.TimeZone},
                    {"before", v.Before},
                    {"after", v.After},
                    {"precision", (int) v.Precision},
                    {"calendarmodel", v.CalendarModel.ToString()}
                };
                return obj;
            });

        // No scientific notation. It's desirable.
        private const string SignedFloatFormat = "+0.#################;-0.#################;0";

        /// <summary>
        /// Literal data field for a quantity that relates to some kind of well-defined unit.
        /// The actual unit goes in the data values that is entered.
        /// </summary>
        /// <remarks>The value type for this data type is `quantity`.</remarks>
        public static WikibaseDataType Quantity { get; } = new DelegatePropertyType<WbQuantity>("quantity",
            e =>
            {
                var amount = Convert.ToDouble((string)e["amount"]);
                var unit = (string)e["unit"];
                var lb = (string)e["lowerBound"];
                var ub = (string)e["upperBound"];
                return new WbQuantity(amount,
                    lb == null ? amount : Convert.ToDouble(lb),
                    ub == null ? amount : Convert.ToDouble(ub),
                    unit == "1" ? WbQuantity.Unity : WikibaseUriFactory.Get(unit));
            }, v =>
            {
                var obj = new JObject
                {
                    {"amount", v.Amount.ToString(SignedFloatFormat)},
                    {"unit", v.Unit.ToString()},
                };
                if (v.HasError)
                {
                    obj.Add("lowerBound", v.LowerBound.ToString(SignedFloatFormat));
                    obj.Add("upperBound", v.UpperBound.ToString(SignedFloatFormat));
                }
                return obj;
            });

        /// <summary>
        /// Literal data field for a string that is not translated into other languages.
        /// This type of string is defined once and reused across all languages.
        /// Typical use is a geographical names written in the local language, an identifier of some kind,
        /// a chemical formula or a Latin scientific name.
        /// </summary>
        /// <remarks>
        /// The value type for this data type is `monolingualtext`.
        /// </remarks>
        public static WikibaseDataType MonolingualText { get; }
            = new DelegatePropertyType<WbMonolingualText>("monolingualtext",
                e => new WbMonolingualText((string)e["language"], (string)e["text"]),
                v => new JObject { { "text", v.Text }, { "language", v.Language } });

        /// <summary>
        /// Literal data field for mathematical expressions, formula, equations and such, expressed in a variant of LaTeX.
        /// </summary>
        /// <remarks>
        /// The value type for this data type is `string`.
        /// </remarks>
        public static WikibaseDataType Math { get; }
            = new DelegatePropertyType<string>("math", "string",
            e => (string)e, v => v);

        /// <summary>
        /// Literal data field for a musical score written in LilyPond notation.
        /// </summary>
        /// <remarks>
        /// The value type for this data type is `string`.
        /// </remarks>
        public static WikibaseDataType MusicalNotation { get; }
            = new DelegatePropertyType<string>("musical-notation", "string",
            e => (string)e, v => v);

        /// <summary>
        /// Literal data field for an external identifier.
        /// External identifiers may automatically be linked to an authoritative resource for display.
        /// </summary>
        public static WikibaseDataType ExternalId { get; }
            = new DelegatePropertyType<string>("external-id", "string",
                e => (string)e, v => v);

        public static WikibaseDataType GlobeCoordinate { get; }
            = new DelegatePropertyType<WbGlobeCoordinate>("globe-coordinate", "globecoordinate",
                e => new WbGlobeCoordinate((double)e["latitude"], (double)e["longitude"],
                    (double)e["precision"], WikibaseUriFactory.Get((string)e["globe"])),
                v => new JObject
                {
                    {"latitude", v.Latitude}, {"longitude", v.Longitude},
                    {"precision", v.Precision}, {"globe", v.Globe.ToString()},
                });

        /// <summary>
        /// Link to geographic map data stored on Wikimedia Commons (or other configured wiki).
        /// See "https://www.mediawiki.org/wiki/Help:Map_Data" for more documentation about map data.
        /// </summary>
        public static WikibaseDataType GeoShape { get; } = new DelegatePropertyType<string>("geo-shape", "string",
            e => (string)e, v => v);

        /// <summary>
        /// Link to tabular data stored on Wikimedia Commons (or other configured wiki).
        /// See "https://www.mediawiki.org/wiki/Help:Tabular_Data" for more documentation about tabular data.
        /// </summary>
        public static WikibaseDataType TabularData { get; } = new DelegatePropertyType<string>("tabular-data", "string",
            e => (string)e, v => v);

        private static readonly Dictionary<string, WikibaseDataType> typeDict = new Dictionary<string, WikibaseDataType>();

        static BuiltInDataTypes()
        {
            foreach (var p in typeof(BuiltInDataTypes).GetProperties()
                .Where(p => p.PropertyType == typeof(WikibaseDataType)))
            {
                var value = (WikibaseDataType)p.GetValue(null);
                typeDict.Add(value.Name, value);
            }
        }

        /// <summary>
        /// Tries to get a property type by its name.
        /// </summary>
        /// <param name="typeName">Internal name of the desired property type.</param>
        /// <exception cref="ArgumentNullException"><paramref name="typeName"/> is <c>null</c>.</exception>
        /// <returns>The matching type, or <c>null</c> if cannot find one.</returns>
        public static WikibaseDataType Get(string typeName)
        {
            if (typeName == null) throw new ArgumentNullException(nameof(typeName));
            if (typeDict.TryGetValue(typeName, out var t)) return t;
            return null;
        }

    }
}
