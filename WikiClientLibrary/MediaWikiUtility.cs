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
using WikiClientLibrary.Infrastructures.Logging;

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
        public static async Task<string?> SearchApiEndpointAsync(WikiClient client, string urlExpression)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (urlExpression == null) throw new ArgumentNullException(nameof(urlExpression));
            urlExpression = urlExpression.Trim();
            if (urlExpression.Length == 0) return null;
            using (client.BeginActionScope(null))
            {
                client.Logger.LogInformation("Search MediaWiki API endpoint starting from {Url}.", urlExpression);
                // Directly try the given URL.
                var current = await TestApiEndpointAsync(client, urlExpression);
                if (current != null) return current;
                // Try to infer from the page content.
                var (url, content) = await DownloadStringAsync(client, urlExpression, true);
                if (url != null && !string.IsNullOrEmpty(content))
                {
                    current = url;
                    // <link rel="EditURI" type="application/rsd+xml" href="http://..../api.php?action=rsd"/>
                    var match = Regex.Match(content, @"(?<=href\s*=\s*[""']?)[^\?""']+(?=\?action=rsd)");
                    if (match.Success)
                    {
                        var v = NavigateTo(current, match.Value);
                        v = await TestApiEndpointAsync(client, v);
                        if (v != null) return v;
                    }
                }
                return null;
            }
        }

        private static async Task<(string? FinalUrl, string? Content)> DownloadStringAsync(WikiClient client, string url, bool accept400)
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
            var status = (int)resp.StatusCode;
            if (status == 200 || (accept400 && status >= 400 && status < 500))
            {
                var fianlUrl = resp.RequestMessage.RequestUri.ToString();
                var content = await resp.Content.ReadAsStringAsync();
                return (fianlUrl, content);
            }
            return (null, null);
        }

        /// <summary>
        /// Tests whether the specific URL is a valid MediaWiki API endpoint, and
        /// returns the final URL, if redirected.
        /// </summary>
        private static async Task<string?> TestApiEndpointAsync(WikiClient client, string url)
        {
            // Append default protocol.
            if (!ProtocolMatcher.IsMatch(url))
                url = "http://" + url;
            // Resolve relative protocol.
            else if (url.StartsWith("//"))
                url = "http:" + url;
            try
            {
                client.Logger.LogDebug("Test MediaWiki API endpoint: {Url}.", url);
                var (finalUrl, content) = await DownloadStringAsync(client, url + "?action=query&format=json", false);
                if (finalUrl == null) return null;
                // Ref: {"batchcomplete":""}
                if (string.IsNullOrEmpty(content) || content.Length < 2) return null;
                if (content[0] != '{' && content[0] != '[') return null;
                JToken.Parse(content);
                // Remove query string in the result
                var querySplitter = finalUrl.IndexOf('?');
                if (querySplitter > 0) finalUrl = finalUrl.Substring(0, querySplitter);
                client.Logger.LogInformation("Found MediaWiki API endpoint at: {Url}.", finalUrl);
                return finalUrl;
            }
            catch (JsonException)
            {
                return null;
            }
        }

    }
}