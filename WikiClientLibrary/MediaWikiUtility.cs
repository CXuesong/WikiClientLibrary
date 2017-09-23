using System;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;

namespace WikiClientLibrary
{
    internal static class MediaWikiUtility
    {
        private static readonly Regex ProtocolMatcher = new Regex(@"^[A-Za-z\-]+(?=://)");

        /// <summary>
        /// Navigate to the specific URL, taking base URL into consideration.
        /// </summary>
        private static string NavigateTo(string baseUrl, string url)
        {
            if (baseUrl == null) throw new ArgumentNullException(nameof(baseUrl));
            if (url == null) throw new ArgumentNullException(nameof(url));
            var baseUri = new Uri(baseUrl);
            var uri = new Uri(baseUri, url);
            return uri.ToString();
        }

        // See Site.SearchApiEndpointAsync .
        public static async Task<string> SearchApiEndpointAsync(WikiClient client, string urlExpression, ILogger logger)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (urlExpression == null) throw new ArgumentNullException(nameof(urlExpression));
            urlExpression = urlExpression.Trim();
            if (urlExpression == "") return null;
            // Directly try the given URL.
            var current = await TestApiEndpointAsync(client, urlExpression, logger);
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
                    v = await TestApiEndpointAsync(client, v, logger);
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
        private static async Task<string> TestApiEndpointAsync(WikiClient client, string url, ILogger logger)
        {
            // Append default protocol.
            if (!ProtocolMatcher.IsMatch(url))
                url = "http://" + url;
            // Resolve relative protocol.
            else if (url.StartsWith("//"))
                url = "http:" + url;
            try
            {
                logger.LogDebug("Test MediaWiki API endpoint: {Url}.", url);
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