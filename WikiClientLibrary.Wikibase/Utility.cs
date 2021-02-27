using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Wikibase
{
    internal static class Utility
    {

        internal static readonly JsonSerializer WikiJsonSerializer = MediaWikiHelper.CreateWikiJsonSerializer();

        /// <summary>
        /// Partitions <see cref="IEnumerable{T}"/> into a sequence of <see cref="IEnumerable{T}"/>,
        /// each child <see cref="IEnumerable{T}"/> having the same length, except the last one.
        /// </summary>
        public static IEnumerable<IReadOnlyCollection<T>> Partition<T>(this IEnumerable<T> source, int partitionSize)
        {
            if (partitionSize <= 0) throw new ArgumentOutOfRangeException(nameof(partitionSize));
            var list = new List<T>(partitionSize);
            foreach (var item in source)
            {
                list.Add(item);
                if (list.Count == partitionSize)
                {
                    yield return list;
                    list.Clear();
                }
            }
            if (list.Count > 0) yield return list;
        }

        public static JObject ToJObject<TSource>(this IEnumerable<TSource> source,
            Func<TSource, string> propertyNameSelector, Func<TSource, JToken> valueSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (propertyNameSelector == null) throw new ArgumentNullException(nameof(propertyNameSelector));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            var obj = new JObject();
            foreach (var item in source) obj.Add(propertyNameSelector(item), valueSelector(item));
            return obj;
        }

        public static JArray ToJArray<TSource>(this IEnumerable<TSource> source)
        {
            var arr = new JArray(source);
            return arr;
        }

        public static string Serialize(this JsonSerializer serializer, object value)
        {
            using var writer = new StringWriter();
            serializer.Serialize(writer, value);
            return writer.ToString();
        }

        public static T? Deserialize<T>(this JsonSerializer serializer, TextReader reader) where T : class
        {
            using var jreader = new JsonTextReader(reader);
            return serializer.Deserialize<T>(jreader);
        }

        public static T? Deserialize<T>(this JsonSerializer serializer, string json) where T : class
        {
            using var reader = new StringReader(json);
            return serializer.Deserialize<T>(reader);
        }

        public static string NewClaimGuid(string entityId)
        {
            return entityId + "$" + Guid.NewGuid().ToString("D");
        }

    }
}
