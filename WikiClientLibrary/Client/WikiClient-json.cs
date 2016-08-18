using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace WikiClientLibrary.Client
{
    partial class WikiClient
    {
        #region the json client

        private async Task<JObject> SendAsync(WebRequest request)
        {
            var retries = 0;
            RETRY:
            var responseTask = request.GetResponseAsync();
            using (var timeoutCancellation = new CancellationTokenSource(Timeout))
            {
                var timeoutTask = Task.Delay(Timeout, timeoutCancellation.Token);
                if (await Task.WhenAny(responseTask, timeoutTask) == timeoutTask)
                {
                    if (!responseTask.IsCompleted)
                    {
                        //To time out asynchronous requests, use the Abort method.
                        try
                        {
                            request.Abort();
                        }
                        catch (WebException ex)
                        {
                            Debug.Assert(ex.Status == WebExceptionStatus.RequestCanceled);
                        }
                        retries++;
                        Logger?.Warn($"Timeout(x{retries}): {request.RequestUri}");
                        //timeoutTask.Dispose();
                        if (retries > MaxRetries) throw new TimeoutException();
                        // Before retry, copy the request.
                        var newRequest = WebRequest.Create(request.RequestUri);
                        newRequest.Method = request.Method;
                        //newRequest.Timeout = request.Timeout; of no use.
                        var hwr = (HttpWebRequest) newRequest;
                        hwr.SetHeader("User-Agent", request.Headers[HttpRequestHeader.UserAgent]);
                        hwr.SetHeader("Referer", request.Headers[HttpRequestHeader.Referer]);
                        request = newRequest;
                        goto RETRY;
                    }
                }
                // The request has finished.
                timeoutCancellation.Cancel();
            }
            // Use await instead of responseTask.Result to unwrap Exceptions.
            // Or AggregateException might be thrown.
            try
            {
                var result = (HttpWebResponse)await responseTask;
                Logger?.Trace($"{(int) result.StatusCode}[{result.StatusDescription}]: {request.RequestUri}");
                return ProcessAsyncResponse(result);
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, request.RequestUri.ToString());
                throw;
            }
        }

        private JObject ProcessAsyncResponse(HttpWebResponse webResponse)
        {
            using (webResponse)
            {
                if (webResponse.StatusCode == HttpStatusCode.OK ||
                    webResponse.StatusCode == HttpStatusCode.Accepted ||
                    webResponse.StatusCode == HttpStatusCode.Created)
                {
                    if (webResponse.ContentLength != 0)
                    {
                        using (var stream = webResponse.GetResponseStream())
                        {
                            if (stream != null)
                            {
                                using (var reader = new StreamReader(stream))
                                using (var jreader = new JsonTextReader(reader))
                                    return JObject.Load(jreader);
                            }
                        }
                    }
                }
            }
            Logger?.Warn("No data received.");
            return null;
        }
        #endregion
    }

    internal class WikiJsonContractResolver : DefaultContractResolver
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
    }
}
