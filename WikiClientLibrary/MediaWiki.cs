using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace WikiClientLibrary
{

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
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
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

    /// <summary>
    /// The data used for continuation of query.
    /// </summary>
    public struct ContinuationToken
    {
        /// <summary>
        /// Represents an empty <see cref="ContinuationToken"/>.
        /// </summary>
        public static readonly ContinuationToken Empty = new ContinuationToken();

        public IReadOnlyCollection<KeyValuePair<string, string>> ContinueParams { get; }

        public bool IsEmpty => ContinueParams == null;

        internal ContinuationToken(JObject continueParams)
        {
            if (continueParams == null) throw new ArgumentNullException(nameof(continueParams));
            ContinueParams = new ReadOnlyCollection<KeyValuePair<string, string>>(
                continueParams.Properties().Select(p =>
                    new KeyValuePair<string, string>(p.Name, (string) p.Value)).ToList());
        }
    }

    /// <summary>
    /// Contains MediaWiki built-in namespace ids for most MediaWiki site. (MediaWiki 1.14+)
    /// </summary>
    public static class BuiltInNamespaces
    {
        public const int Media = -2;
        public const int Special = -1;
        public const int Main = 0;
        public const int Talk = 1;
        public const int User = 2;
        public const int UserTalk = 3;
        public const int Project = 4;
        public const int ProjectTalk = 5;
        public const int File = 6;
        public const int FileTalk = 7;
        public const int MediaWiki = 8;
        public const int MediaWikiTalk = 9;
        public const int Template = 10;
        public const int TemplateTalk = 11;
        public const int Help = 12;
        public const int HelpTalk = 13;
        public const int Category = 14;
        public const int CategoryTalk = 15;

        private static readonly IDictionary<int, string> _CanonicalNameDict = new Dictionary<int, string>
        {
            {-2, "Media"},
            {-1, "Special"},
            {0, ""},
            {1, "Talk"},
            {2, "User"},
            {3, "User talk"},
            {4, "Project"},
            {5, "Project talk"},
            {6, "File"},
            {7, "File talk"},
            {8, "MediaWiki"},
            {9, "MediaWiki talk"},
            {10, "Template"},
            {11, "Template talk"},
            {12, "Help"},
            {13, "Help talk"},
            {14, "Category"},
            {15, "Category talk"},
        };

        /// <summary>
        /// Gets the canonical name for a specific built-in namespace.
        /// </summary>
        /// <returns>
        /// canonical name for the specified built-in namespace.
        /// OR <c>null</c> if no such namespace is found.
        /// </returns>
        public static string GetCanonicalName(int namespaceId)
        {
            string name;
            if (_CanonicalNameDict.TryGetValue(namespaceId, out name)) return name;
            return null;
        }
    }
}
