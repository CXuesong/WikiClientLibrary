using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace WikiClientLibrary.Infrastructures
{
    /// <summary>
    /// Helper methods for extending MW API.
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
        /// <param name="defaultProtocol">For protocol-relative URL, (e.g. <c>//en.wikipedia.org/</c>)
        /// specifies the default protocol to use. (e.g. <c>https:</c>)</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="relativeProtocolUrl"/> or <paramref name="defaultProtocol"/> is <c>null</c>.</exception>
        /// <returns>The URL with absolute protocol. If the specified URL is not a relative protocol URL,
        /// it will be returned directly.</returns>
        public static string MakeAbsoluteProtocol(string relativeProtocolUrl, string defaultProtocol)
        {
            if (relativeProtocolUrl == null) throw new ArgumentNullException(nameof(relativeProtocolUrl));
            if (defaultProtocol == null) throw new ArgumentNullException(nameof(defaultProtocol));
            var url = relativeProtocolUrl;
            if (url.StartsWith("//")) url = defaultProtocol + url;
            return url;
        }

        /// <summary>
        /// Combines a base URL and a relative URL, using <c>https:</c> for relative protocol URL.
        /// </summary>
        /// <param name="baseUrl">The base absolute URL. Can be relative protocol URL.</param>
        /// <param name="relativeUrl">The relative URL.</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="baseUrl"/> or <paramref name="relativeUrl"/> is <c>null</c>.</exception>
        /// <returns>The combined URL with absolute protocol.</returns>
        public static string MakeAbsoluteUrl(string baseUrl, string relativeUrl)
        {
            return MakeAbsoluteUrl(baseUrl, relativeUrl, "https:");
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

    }
}
