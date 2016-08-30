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
using WikiClientLibrary.Generators;

namespace WikiClientLibrary
{
    internal static class Utility
    {

        // http://stackoverflow.com/questions/36186276/is-the-json-net-jsonserializer-threadsafe
        public static readonly JsonSerializer WikiJsonSerializer = CreateWikiJsonSerializer();

        /// <summary>
        /// Create an new instance of <see cref="JsonSerializer"/> with its
        /// own instance of <see cref="JsonSerializerSettings"/>.
        /// </summary>
        public static JsonSerializer CreateWikiJsonSerializer()
        {
            var settings =
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
            return JsonSerializer.CreateDefault(settings);
        }

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

        private static string ToWikiQueryValue(object value)
        {
            if (value == null)
                return null;
            if (value is bool)
            {
                if ((bool) value) value = "";
                else return null;
            }
            else if (value is AutoWatchBehavior)
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
                        throw new ArgumentOutOfRangeException(nameof(value), value, null);
                }
            }
            else if (value is DateTime)
            {
                // ISO 8601
                value = ((DateTime)value).ToString("yyyy-MM-ddTHH:mm:ssK");
            }
            return Convert.ToString(value);
        }

        private static IEnumerable<KeyValuePair<string, string>> IterateWikiStringValuePairs(object values)
        {
            Debug.Assert(!(values is IEnumerable<KeyValuePair<string, string>>));
            var dict = values as IDictionary<string, object>;
            if (dict != null)
                return from p in dict
                    let value = ToWikiQueryValue(p.Value)
                    where value != null
                    select new KeyValuePair<string, string>(p.Key, value);
            return from p in values.GetType().GetRuntimeProperties()
                   let value = ToWikiQueryValue(p.GetValue(values))
                   where value != null select new KeyValuePair<string, string>(p.Name, value);
        }

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

        public static string ToString(this PropertyFilterOption value,
            string withValue, string withoutValue, string allValue = "all")
        {
            switch (value)
            {
                case PropertyFilterOption.Disable:
                    return allValue;
                case PropertyFilterOption.WithProperty:
                    return withValue;
                case PropertyFilterOption.WithoutProperty:
                    return withoutValue;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }

        /// <summary>
        /// Normalizes part of title (either namespace name or page title, not both)
        /// to its cannonical form.
        /// </summary>
        /// <param name="title">The title to be normalized.</param>
        /// <param name="caseSensitive">Whether the title is case sensitive.</param>
        /// <returns>
        /// Normalized part of title. The underscores are replaced by spaces,
        /// and when <paramref name="caseSensitive"/> is <c>true</c>, the first letter is
        /// upper-case. Multiple spaces will be replaced with a single space. Lading
        /// and trailing spaces will be removed.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="title"/> is <c>null</c>.</exception>
        public static string NormalizeTitlePart(string title, bool caseSensitive)
        {
            // Reference to Pywikibot. page.py, Link class.
            if (title == null) throw new ArgumentNullException(nameof(title));
            if (title.Length == 0) return title;
            var state = 0;
            /*
             STATE
             0      Leading whitespace
             1      In title, after non-whitespace
             2      In title, after whitespace
             */
            var sb = new StringBuilder();
            foreach (var c in title)
            {
                var isWhitespace = c == ' ' || c == '_';
                // Remove left-to-right and right-to-left markers.
                if (c == '\u200e' || c == '\u200f') continue;
                switch (state)
                {
                    case 0:
                        if (!isWhitespace)
                        {
                            sb.Append(caseSensitive ? c : char.ToUpperInvariant(c));
                            state = 1;
                        }
                        break;
                    case 1:
                        if (isWhitespace)
                        {
                            sb.Append(' ');
                            state = 2;
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                    case 2:
                        if (!isWhitespace)
                        {
                            sb.Append(c);
                            state = 1;
                        }
                        break;
                }
            }
            if (state == 2)
            {
                // Remove trailing space.
                Debug.Assert(sb[sb.Length - 1] == ' ');
                return sb.ToString(0, sb.Length - 1);
            }
            return sb.ToString();
        }

        public static IAsyncEnumerable<TResult> SelectAsync<TSource, TResult>(this IEnumerable<TSource> source,
            Func<TSource, Task<TResult>> selector)
        {
            var enu = source.GetEnumerator();
            return new DelegateAsyncEnumerable<TResult>(async () =>
            {
                if (!enu.MoveNext()) return null;
                var result = await selector(enu.Current);
                return Tuple.Create(result, true);
            });
        }
    }
}
