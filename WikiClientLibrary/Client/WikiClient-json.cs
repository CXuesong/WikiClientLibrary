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

        private async Task<JToken> SendAsync(Func<HttpRequestMessage> requestFactory, bool allowRetry)
        {
            HttpResponseMessage response;
            var retries = -1;
            RETRY:
            var request = requestFactory();
            Debug.Assert(request != null);
            retries++;
            if (retries > 0)
                Logger?.Trace($"Retry x{retries}: {request.RequestUri}");
            try
            {
                // Use await instead of responseTask.Result to unwrap Exceptions.
                // Or AggregateException might be thrown.
                using (var responseCancellation = new CancellationTokenSource(Timeout))
                    response = await _HttpClient.SendAsync(request, responseCancellation.Token);
                // The request has been finished.
            }
            catch (OperationCanceledException)
            {
                if (!allowRetry || retries >= MaxRetries) throw new TimeoutException();
                Logger?.Warn($"Timeout: {request.RequestUri}");
                await Task.Delay(RetryDelay);
                goto RETRY;
            }
            Logger?.Trace($"{response.StatusCode}: {request.RequestUri}");
            var statusCode = (int) response.StatusCode;
            if (statusCode >= 500 && statusCode <= 599)
            {
                // Service Error. We can retry.
                // HTTP 503 : https://www.mediawiki.org/wiki/Manual:Maxlag_parameter
                if (allowRetry && retries < MaxRetries)
                {
                    var date = response.Headers.RetryAfter?.Date;
                    var offset = response.Headers.RetryAfter?.Delta;
                    if (offset == null && date != null) offset = date - DateTimeOffset.Now;
                    if (offset != null)
                    {
                        if (offset > RetryDelay) offset = RetryDelay;
                        await Task.Delay(offset.Value);
                        goto RETRY;
                    }
                }
            }
            response.EnsureSuccessStatusCode();
            var jresp = await ProcessResponseAsync(response);
            CheckErrors(jresp);
            return jresp;
        }

        private async Task<JToken> ProcessResponseAsync(HttpResponseMessage webResponse)
        {
            using (webResponse)
            {
                using (var stream = await webResponse.Content.ReadAsStreamAsync())
                {
                    using (var reader = new StreamReader(stream))
                    using (var jreader = new JsonTextReader(reader))
                    {
                        var obj = JToken.Load(jreader);
                        //Logger?.Trace(obj.ToString());
                        return obj;
                    }
                }
            }
        }

        private void CheckErrors(JToken jresponse)
        {
            var obj = jresponse as JObject;
            if (obj == null) return;
            if (jresponse["warnings"] != null)
            {
                Logger?.Warn(jresponse["warnings"].ToString());
            }
            if (jresponse["error"] != null)
            {
                var err = jresponse["error"];
                var errcode = (string)err["code"];
                var errmessage = ((string)err["info"] + ". " + (string)err["*"]).Trim();
                switch (errcode)
                {
                    case "permissiondenied":
                        throw new UnauthorizedOperationException(errmessage);
                    case "unknown_action":
                        throw new InvalidActionException(errcode, errmessage);
                    default:
                        if (errcode.EndsWith("conflict"))
                            throw new OperationConflictException(errcode, errmessage);
                        throw new OperationFailedException(errcode, errmessage);
                }
            }
        }

        #endregion
    }
}
