using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using WikiClientLibrary.Client;

namespace WikiClientLibrary
{
    internal static class Utility
    {
        // http://stackoverflow.com/questions/36186276/is-the-json-net-jsonserializer-threadsafe
        private static readonly JsonSerializerSettings WikiJsonSerializerSettings =
            new JsonSerializerSettings
            {
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                NullValueHandling = NullValueHandling.Include,
                ContractResolver = new DefaultContractResolver {NamingStrategy = new WikiJsonNamingStrategy()},
                Converters =
                {
                    new WikiBooleanJsonConverter()
                },
            };

        public static readonly JsonSerializer WikiJsonSerializer =
            JsonSerializer.CreateDefault(WikiJsonSerializerSettings);

        /// <summary>
        /// Convert name-value paris to URL query format.
        /// This overload handles <see cref="ExpandoObject"/> as well as anonymous objects.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The key-value pair with null value will be excluded. To specify a key with empty value,
        /// consider using <see cref="string.Empty"/> .
        /// </para>
        /// <para>
        /// For <see cref="bool"/> values, if the value is true, a pair with key and empty value
        /// will be generated; otherwise the whole pair will be excluded. 
        /// </para>
        /// <para>
        /// If <paramref name="values"/> is <see cref="IEnumerable{T}"/> of <see cref="KeyValuePair{TKey,TValue}"/>
        /// of strings, the values will be returned with no further processing.
        /// </para>
        /// </remarks>
        public static IEnumerable<KeyValuePair<string, string>> ToWikiStringValuePairs(object values)
        {
            var pc = values as IEnumerable<KeyValuePair<string, string>>;
            if (pc != null) return pc;
            return IterateWikiStringValuePairs(values);
        }

        private static IEnumerable<KeyValuePair<string, string>> IterateWikiStringValuePairs(object values)
        {
            Debug.Assert(!(values is IEnumerable<KeyValuePair<string, string>>));
            foreach (var p in values.GetType().GetRuntimeProperties())
            {
                var value = p.GetValue(values);
                if (value == null) continue;
                if (value is bool)
                {
                    if ((bool)value) value = "";
                    else continue;
                } else if (value is AutoWatchBehavior)
                {
                    switch ((AutoWatchBehavior)value)
                    {
                        case AutoWatchBehavior.Default:
                            value = "preferences";
                            break;
                        case AutoWatchBehavior.None:
                            value = "nochange";
                            break;
                        case AutoWatchBehavior.Watch:
                            value = "watch";
                            break;
                        case AutoWatchBehavior.Unwatch:
                            value = "unwatch";
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(p.Name, value, null);
                    }
                } else if (value is DateTime)
                {
                    // ISO 8601
                    value = ((DateTime) value).ToString("yyyy-MM-ddTHH:mm:ssK");
                }
                yield return new KeyValuePair<string, string>(p.Name, Convert.ToString(value));
            }
        }
    }

    public class WikiJsonNamingStrategy : NamingStrategy
    {
        /// <summary>
        /// Resolves the specified property name.
        /// </summary>
        /// <param name="name">The property name to resolve.</param>
        /// <returns>
        /// The resolved property name.
        /// </returns>
        protected override string ResolvePropertyName(string name)
        {
            return name.ToLowerInvariant();
        }
    }

    /// <summary>
    /// Handles MediaWiki boolean values.
    /// </summary>
    /// <remarks>
    /// See https://www.mediawiki.org/wiki/API:Data_formats#Boolean_values .
    /// </remarks>
    public class WikiBooleanJsonConverter : JsonConverter
    {
        /// <summary>
        /// Writes the JSON representation of the object.
        /// </summary>
        /// <param name="writer">The <see cref="T:Newtonsoft.Json.JsonWriter"/> to write to.</param><param name="value">The value.</param><param name="serializer">The calling serializer.</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if ((bool) value) writer.WriteValue("");
        }

        /// <summary>
        /// Reads the JSON representation of the object.
        /// </summary>
        /// <param name="reader">The <see cref="T:Newtonsoft.Json.JsonReader"/> to read from.</param><param name="objectType">Type of the object.</param><param name="existingValue">The existing value of object being read.</param><param name="serializer">The calling serializer.</param>
        /// <returns>
        /// The object value.
        /// </returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return existingValue != null;
        }

        /// <summary>
        /// Determines whether this instance can convert the specified object type.
        /// </summary>
        /// <param name="objectType">Type of the object.</param>
        /// <returns>
        /// <c>true</c> if this instance can convert the specified object type; otherwise, <c>false</c>.
        /// </returns>
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof (bool);
        }
    }

    public class WikiJsonContractResolver : DefaultContractResolver
    {
        /// <summary>
        /// Resolves the name of the property.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        /// <returns>
        /// Resolved name of the property.
        /// </returns>
        protected override string ResolvePropertyName(string propertyName)
        {
            return propertyName.ToLowerInvariant();
        }

        /// <summary>
        /// Creates a <see cref="T:Newtonsoft.Json.Serialization.JsonProperty"/> for the given <see cref="T:System.Reflection.MemberInfo"/>.
        /// </summary>
        /// <param name="memberSerialization">The member's parent <see cref="T:Newtonsoft.Json.MemberSerialization"/>.</param><param name="member">The member to create a <see cref="T:Newtonsoft.Json.Serialization.JsonProperty"/> for.</param>
        /// <returns>
        /// A created <see cref="T:Newtonsoft.Json.Serialization.JsonProperty"/> for the given <see cref="T:System.Reflection.MemberInfo"/>.
        /// </returns>
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            //TODO: Maybe cache
            var prop = base.CreateProperty(member, memberSerialization);
            if (!prop.Writable)
            {
                var property = member as PropertyInfo;
                if (property != null)
                {
                    var hasPrivateSetter = property.SetMethod != null;
                    prop.Writable = hasPrivateSetter;
                }
            }
            return prop;
        }
    }
}
