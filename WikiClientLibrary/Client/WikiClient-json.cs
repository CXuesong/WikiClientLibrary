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

        private async Task<JObject> SendAsync(Func<HttpRequestMessage> requestFactory)
        {
            HttpResponseMessage response;
            RETRY:
            var request = requestFactory();
            Debug.Assert(request != null);
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
                            var errcode = (string) err["code"];
                            var errmessage = ((string) err["info"] + " " + (string) err["*"]).Trim();
                            switch (errcode)
                            {
                                case "unknown_action":
                                    throw new InvalidActionException(errcode, errmessage);
                                default:
                                    if (errcode.EndsWith("conflict"))
                                        throw new OperationConflictException(errcode, errmessage);
                                    throw new OperationFailedException(errcode, errmessage);
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
