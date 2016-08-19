using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
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

        private async Task<JObject> SendAsync(HttpRequestMessage request)
        {
            HttpResponseMessage response;
            RETRY:
            var retries = 0;
            using (var responseCancellation = new CancellationTokenSource(Timeout))
            {
                try
                {
                    response = await _HttpClient.SendAsync(request, responseCancellation.Token);
                    // The request has been finished.
                }
                catch (OperationCanceledException)
                {
                    retries++;
                    if (retries > MaxRetries) throw new TimeoutException();
                    Logger?.Warn($"Timeout(x{retries}): {request.RequestUri}");
                    goto RETRY;
                }
            }
            // Use await instead of responseTask.Result to unwrap Exceptions.
            // Or AggregateException might be thrown.
            Logger?.Trace($"{response.StatusCode}: {request.RequestUri}");
            response.EnsureSuccessStatusCode();
            return await ProcessResponseAsync(response);
        }

        private async Task<JObject> ProcessResponseAsync(HttpResponseMessage webResponse)
        {
            using (webResponse)
            {
                using (var stream = await webResponse.Content.ReadAsStreamAsync())
                {
                    using (var reader = new StreamReader(stream))
                    using (var jreader = new JsonTextReader(reader))
                    {
                        var obj = JObject.Load(jreader);
                        //Logger?.Trace(obj.ToString());
                        if (obj["warnings"] != null)
                        {
                            Logger?.Warn(obj["warnings"].ToString());
                        }
                        if (obj["error"] != null)
                        {
                            var err = obj["error"];
                            switch ((string) err["code"])
                            {
                                case "unknown_action":
                                    throw new InvalidActionException((string) err["info"]);
                                default:
                                    throw new OperationFailedException((string) err["code"],
                                        string.Format("{0} {1}", (string) err["info"], (string) err["*"]));
                            }
                        }
                        return obj;
                    }
                }
            }
        }

        #endregion
    }
}
