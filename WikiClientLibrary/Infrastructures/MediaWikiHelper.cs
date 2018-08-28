using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Files;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Pages.Queries;

namespace WikiClientLibrary.Infrastructures
{
    /// <summary>
    /// Helper methods for extending MediaWiki API.
    /// </summary>
    public static class MediaWikiHelper
    {
        /// <summary>
        /// Create an new instance of <see cref="JsonSerializer"/> for parsing MediaWiki API response.
        /// </summary>
        public static JsonSerializer CreateWikiJsonSerializer()
        {
            return Utility.CreateWikiJsonSerializer();
        }

        /// <summary>
        /// Converts the specified relative protocol URL (starting with <c>//</c>) to absolute protocol URL.
        /// </summary>
        /// <param name="relativeProtocolUrl">The URL to be converted.</param>
        /// <param name="defaultProtocol">For protocol-relative URL,(e.g. <c>//en.wikipedia.org/</c>),
        /// specifies the default protocol to use. (e.g. <c>https</c>)</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="relativeProtocolUrl"/> or <paramref name="defaultProtocol"/> is <c>null</c>.</exception>
        /// <returns>The URL with absolute protocol. If the specified URL is not a relative protocol URL,
        /// it will be returned directly.</returns>
        public static string MakeAbsoluteProtocol(string relativeProtocolUrl, string defaultProtocol)
        {
            if (relativeProtocolUrl == null) throw new ArgumentNullException(nameof(relativeProtocolUrl));
            if (defaultProtocol == null) throw new ArgumentNullException(nameof(defaultProtocol));
            var url = relativeProtocolUrl;
            if (url.StartsWith("//")) url = defaultProtocol + ":" + url;
            return url;
        }

        /// <summary>
        /// Combines a base URL and a relative URL, using <c>https</c> for relative protocol URL.
        /// </summary>
        /// <param name="baseUrl">The base absolute URL. Can be relative protocol URL.</param>
        /// <param name="relativeUrl">The relative URL.</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="baseUrl"/> or <paramref name="relativeUrl"/> is <c>null</c>.</exception>
        /// <returns>The combined URL with absolute protocol.</returns>
        public static string MakeAbsoluteUrl(string baseUrl, string relativeUrl)
        {
            return MakeAbsoluteUrl(baseUrl, relativeUrl, "https");
        }

        /// <summary>
        /// Combines a base URL and a relative URL, using the specified protocol for relative protocol URL.
        /// </summary>
        /// <param name="baseUrl">The base absolute URL. Can be relative protocol URL.</param>
        /// <param name="relativeUrl">The relative URL.</param>
        /// <param name="defaultProtocol">For protocol-relative URL, (e.g. <c>//en.wikipedia.org/</c>)
        /// specifies the default protocol to use. (e.g. <c>https:</c>)</param>
        /// <exception cref="ArgumentNullException"><paramref name="baseUrl"/>, <paramref name="relativeUrl"/>,
        /// or <paramref name="defaultProtocol"/> is <c>null</c>.</exception>
        /// <returns>The combined URL with absolute protocol.</returns>
        public static string MakeAbsoluteUrl(string baseUrl, string relativeUrl, string defaultProtocol)
        {
            if (baseUrl == null) throw new ArgumentNullException(nameof(baseUrl));
            if (relativeUrl == null) throw new ArgumentNullException(nameof(relativeUrl));
            if (defaultProtocol == null) throw new ArgumentNullException(nameof(defaultProtocol));
            baseUrl = MakeAbsoluteProtocol(baseUrl, defaultProtocol);
            return new Uri(new Uri(baseUrl, UriKind.Absolute), relativeUrl).ToString();
        }

        /// <summary>
        /// Enumerates from either a sequence of key-value pairs, or the property-value pairs of an anonymous object.
        /// </summary>
        /// <param name="dict">A <see cref="IEnumerable{T}"/> of <see cref="KeyValuePair{TKey,TValue}"/>,
        /// where <c>TKey</c> should be <see cref="string"/>, while <c>TValue</c> can either be <see cref="string"/> or <see cref="object"/>.
        /// Or an anonymous object, in which case, its properties and values are enumerated.</param>
        /// <returns>A sequence containning the enumerated key-value pairs.</returns>
        public static IEnumerable<KeyValuePair<string, object>> EnumValues(object dict)
        {
            if (dict == null) throw new ArgumentNullException(nameof(dict));
            if (dict is IEnumerable<KeyValuePair<string, object>> objEnu)
                return objEnu;
            if (dict is IEnumerable<KeyValuePair<string, string>> stringEnu)
                return stringEnu.Select(p => new KeyValuePair<string, object>(p.Key, p.Value));
            if (dict is IDictionary idict0)
            {
                IEnumerable<KeyValuePair<string, object>> Enumerator(IDictionary idict)
                {
                    var de = idict.GetEnumerator();
                    while (de.MoveNext()) yield return new KeyValuePair<string, object>((string)de.Key, de.Value);
                }

                return Enumerator(idict0);
            }
            // Sanity check: We only want to marshal anonymous types.
            // If you are in RELEASE mode… I wish you good luck.
            Debug.Assert(dict.GetType().GetTypeInfo().CustomAttributes
                    .Any(a => a.AttributeType != typeof(CompilerGeneratedAttribute)),
                "We only want to marshal anonymous types. Did you accidentally pass in a wrong object?");
            return from p in dict.GetType().GetRuntimeProperties()
                   let value = p.GetValue(dict)
                   select new KeyValuePair<string, object>(p.Name, value);
        }

        public static async Task<JToken> ParseJsonAsync(Stream stream, CancellationToken cancellationToken)
        {
            // TODO buffer stream, instead of reading all
            var content = await stream.ReadAllStringAsync(cancellationToken);
            //Logger?.Trace(content);
            return JToken.Parse(content);
        }

        /// <summary>
        /// Creates a <see cref="WikiPageStub"/> instance from the given raw page information.
        /// </summary>
        /// <param name="jPage">The JSON page-like object.</param>
        /// <exception cref="ArgumentException">The given JSON object contains none of <c>title</c>+<c>ns</c> or <c>pageid</c>.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="jPage"/> is <c>null</c>.</exception>
        /// <returns>The page stub that contains the information given in <paramref name="jPage"/>.</returns>
        /// <remarks>
        /// A typical JSON page-like object has the following structure
        /// <code language="js">
        /// {
        ///     // Basic page information
        ///     "title": "Title",
        ///     "pageud": 1234,
        ///     "ns": 0
        ///     // Page status
        ///     "special": "",
        ///     "missing": "",
        ///     "invalid": ""
        /// }
        /// </code>
        /// A valid JSON page-like object should at least has <c>title</c>+<c>ns</c>, <c>pageid</c>, or both.
        /// The status flag corresponds with <a href="https://www.mediawiki.org/wiki/API:Data_formats#Boolean_values">format specification for Boolean</a>
        /// in MediaWiki API.
        /// </remarks>
        public static WikiPageStub PageStubFromJson(JObject jPage)
        {
            if (jPage == null) throw new ArgumentNullException(nameof(jPage));
            if (jPage["invalid"] != null)
                return WikiPageStub.NewInvalidPage((string)jPage["title"]);
            if (jPage["special"] != null)
                return WikiPageStub.NewSpecialPage((string)jPage["title"], (int)jPage["ns"], jPage["missing"] != null);
            if (jPage["missing"] != null)
            {
                if (jPage["title"] != null)
                    return WikiPageStub.NewMissingPage((string)jPage["title"], (int)jPage["ns"]);
                if (jPage["pageid"] != null)
                    return WikiPageStub.NewMissingPage((int)jPage["pageid"]);
                return WikiPageStub.NewMissingPage(WikiPageStub.MissingPageIdMask);
            }
            if (jPage["pageid"] != null)
            {
                if (jPage["title"] != null)
                    return new WikiPageStub((int)jPage["pageid"], (string)jPage["title"], (int)jPage["ns"]);
                return new WikiPageStub((int)jPage["pageid"]);
            }
            if (jPage["title"] != null)
                return new WikiPageStub((string)jPage["title"], (int)jPage["ns"]);
            throw new ArgumentException("The specified JSON object does not contain MediaWiki page information.", nameof(jPage));
        }

        public static Revision RevisionFromJson(JObject jRevision, WikiPageStub pageStub)
        {
            var rev = jRevision.ToObject<Revision>(Utility.WikiJsonSerializer);
            rev.Page = pageStub;
            return rev;
        }

        public static FileRevision FileRevisionFromJson(JObject jRevision, WikiPageStub pageStub)
        {
            var rev = jRevision.ToObject<FileRevision>(Utility.WikiJsonSerializer);
            rev.Page = pageStub;
            return rev;
        }

        public static GeoCoordinate GeoCoordinateFromJson(JObject jcoordinate)
        {
            return new GeoCoordinate
            {
                Longitude = (double)jcoordinate["lon"],
                Latitude = (double)jcoordinate["lat"],
                Dimension = (double?)jcoordinate["dim"] ?? 0,
                Globe = (string)jcoordinate["globe"],
            };
        }
        
        private static readonly ConcurrentDictionary<PageQueryOptions, IWikiPageQueryProvider> queryProviderPresets
            = new ConcurrentDictionary<PageQueryOptions, IWikiPageQueryProvider>();

        /// <summary>
        /// Gets a read-only implementation of <see cref="IWikiPageQueryProvider"/> for fetching a page.
        /// </summary>
        /// <remarks>
        /// This method returns a shared read-only instance for a specific <see cref="PageQueryOptions"/> value to reduce memory consumption.
        /// If you want to apply your customization based on the presets, use <see cref="WikiPageQueryProvider.FromOptions"/>.
        /// </remarks>
        public static IWikiPageQueryProvider QueryProviderFromOptions(PageQueryOptions options)
        {
            return queryProviderPresets.GetOrAdd(options,
                k => new SealedWikiPageQueryProvider(WikiPageQueryProvider.FromOptions(options)));
        }

        /// <summary>
        /// Loads page information from JSON.
        /// </summary>
        /// <param name="page"></param>
        /// <param name="json">query.pages.xxx property value.</param>
        /// <param name="options"></param>
        public static void PopulatePageFromJson(WikiPage page, JObject json, IWikiPageQueryProvider options)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            if (json == null) throw new ArgumentNullException(nameof(json));
            if (options == null) throw new ArgumentNullException(nameof(options));
            page.OnLoadPageInfo(json, options);
        }

        /// <summary>
        /// Joins multiple values that will be used as parameter value in MediaWiki API request.
        /// </summary>
        /// <param name="values">The values to be joined. <see cref="object.ToString"/> will be invoked before concatenating the sequence.</param>
        /// <exception cref="ArgumentException">The values contain pipe character '|' and Unit Separator '\u001F' at the same time.</exception>
        /// <returns>A string of values joined by pipe character or alternative multiple-value separator (U+001F),
        /// or <see cref="string.Empty"/> if <paramref name="values"/> is empty sequence.</returns>
        public static string JoinValues<T>(IEnumerable<T> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var sb = new StringBuilder();
            var delimiter = '|';
            foreach (var v in values)
            {
                if (sb.Length > 0) sb.Append(delimiter);
                string str = v == null ? null : v.ToString();
                if (str != null)
                {
                    if (delimiter == '|')
                    {
                        if (str.IndexOf('|') >= 0)
                        {
                            sb.Replace('|', '\u001F');
                            sb.Insert(0, '\u001F');
                        }
                    } else if (/* delimiter == '\u001F' && */ str.IndexOf('\u001F') >= 0)
                    {
                        throw new ArgumentException("values cannot contain pipe character '|' and Unit Separator '\\u001F' at the same time.");
                    }
                    sb.Append(str);
                }
            }
            return sb.ToString();
        }

    }
}
