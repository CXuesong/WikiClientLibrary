using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using WikiClientLibrary.Client;

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
    /// Aside from this convention, the converter can also recognize string values such as "false" or "False".
    /// Note this convention is used in Flow extension.
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
            var str = existingValue as string;
            return existingValue != null && !string.Equals(str, "false", StringComparison.OrdinalIgnoreCase);
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
            return objectType == typeof(bool);
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

    /// <summary>
    /// Contains commonly-used content model names for MediaWiki pages. (MediaWiki 1.22+)
    /// </summary>
    public static class ContentModels
    {
        /// <summary>
        /// Normal wikitext.
        /// </summary>
        public const string Wikitext = "wikitext";
        public const string JavaScript = "javascript";
        public const string Css = "css";
        /// <summary>
        /// Scribunto LUA module.
        /// </summary>
        public const string Scribunto = "Scribunto";
        /// <summary>
        /// Flow board page.
        /// </summary>
        /// <remarks>See https://www.mediawiki.org/wiki/Extension:Flow/API .</remarks>
        public const string FlowBoard = "flow-board";
    }

    internal static class MediaWikiUtility
    {
        private static readonly Regex ProtocolMatcher = new Regex(@"^[A-Za-z\-]+(?=://)");

        /// <summary>
        /// Navigate to the specific URL, taking base URL into consideration.
        /// </summary>
        public static string NavigateTo(string baseUrl, string url)
        {
            if (baseUrl == null) throw new ArgumentNullException(nameof(baseUrl));
            if (url == null) throw new ArgumentNullException(nameof(url));
            var baseUri = new Uri(baseUrl);
            var uri = new Uri(baseUri, url);
            return uri.ToString();
        }

        // See Site.SearchApiEndpointAsync .
        public static async Task<string> SearchApiEndpointAsync(WikiClient client, string urlExpression)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (urlExpression == null) throw new ArgumentNullException(nameof(urlExpression));
            urlExpression = urlExpression.Trim();
            if (urlExpression == "") return null;
            // Directly try the given URL.
            var current = await TestApiEndpointAsync(client, urlExpression);
            if (current != null) return current;
            // Try to infer from the page content.
            var result = await DownloadStringAsync(client, urlExpression, true);
            if (result != null)
            {
                current = result.Item1;
                // <link rel="EditURI" type="application/rsd+xml" href="http://..../api.php?action=rsd"/>
                var match = Regex.Match(result.Item2, @"(?<=href\s*=\s*[""']?)[^\?""']+(?=\?action=rsd)");
                if (match.Success)
                {
                    var v = NavigateTo(current, match.Value);
                    v = await TestApiEndpointAsync(client, v);
                    if (v != null) return v;
                }
            }
            return null;
        }

        // Tuple<final URL, downloaded string>
        private static async Task<Tuple<string, string>> DownloadStringAsync(WikiClient client, string url,
            bool accept400)
        {
            const int timeout = 10000;
            HttpResponseMessage resp;
            // Append default protocol.
            if (!ProtocolMatcher.IsMatch(url))
                url = "http://" + url;
            // Resolve relative protocol.
            else if (url.StartsWith("//"))
                url = "http:" + url;
            using (var cts = new CancellationTokenSource(timeout))
            {
                try
                {
                    resp = await client.HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, url), cts.Token);
                }
                catch (TaskCanceledException)
                {
                    throw new TimeoutException();
                }
            }
            var status = (int) resp.StatusCode;
            if (status == 200 || (accept400 && status >= 400 && status < 500) )
            {
                var fianlUrl = resp.RequestMessage.RequestUri.ToString();
                var content = await resp.Content.ReadAsStringAsync();
                return Tuple.Create(fianlUrl, content);
            }
            return null;
        }

        /// <summary>
        /// Tests whether the specific URL is a valid MediaWiki API endpoint, and
        /// returns the final URL, if redirected.
        /// </summary>
        private static async Task<string> TestApiEndpointAsync(WikiClient client, string url)
        {
            // Append default protocol.
            if (!ProtocolMatcher.IsMatch(url))
                url = "http://" + url;
            // Resolve relative protocol.
            else if (url.StartsWith("//"))
                url = "http:" + url;
            try
            {
                client.Logger?.Trace("Test MediaWiki API: " + url);
                var result = await DownloadStringAsync(client, url + "?action=query&format=json", false);
                if (result == null) return null;
                var content = result.Item2;
                // Ref: {"batchcomplete":""}
                if (content.Length < 2) return null;
                if (content[0] != '{' && content[0] != '[') return null;
                JToken.Parse(content);
                var finalUrl = result.Item1;
                // Remove query string in the result
                var querySplitter = finalUrl.IndexOf('?');
                if (querySplitter > 0) return finalUrl.Substring(0, querySplitter);
                return finalUrl;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// This version handles special expressions such as "infinity".
        /// </summary>
        public static DateTime ParseDateTimeOffset(string expression)
        {
            if (expression == null) throw new ArgumentNullException(nameof(expression));
            if (expression == "infinity") return DateTime.MaxValue;
            return DateTime.Parse(expression, null, DateTimeStyles.None);
        }
    }
}
