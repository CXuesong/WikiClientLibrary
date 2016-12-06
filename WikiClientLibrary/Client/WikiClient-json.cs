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

        private async Task<JToken> SendAsync(Func<HttpRequestMessage> requestFactory, bool allowsRetry,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response;
            var retries = -1;
            RETRY:
            cancellationToken.ThrowIfCancellationRequested();
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
                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, responseCancellation.Token))
                    response = await HttpClient.SendAsync(request, linkedCts.Token);
                // The request has been finished.
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Logger?.Warn($"Cancelled: {request.RequestUri}");
                    throw new OperationCanceledException();
                }
                else
                {
                    Logger?.Warn($"Timeout: {request.RequestUri}");
                }
                if (!allowsRetry || retries >= MaxRetries) throw new TimeoutException();
                await Task.Delay(RetryDelay, cancellationToken);
                goto RETRY;
            }
            // Validate response.
            Logger?.Trace($"{response.StatusCode}: {request.RequestUri}");
            var statusCode = (int) response.StatusCode;
            if (statusCode >= 500 && statusCode <= 599)
            {
                // Service Error. We can retry.
                // HTTP 503 : https://www.mediawiki.org/wiki/Manual:Maxlag_parameter
                if (allowsRetry && retries < MaxRetries)
                {
                    var date = response.Headers.RetryAfter?.Date;
                    var offset = response.Headers.RetryAfter?.Delta;
                    if (offset == null && date != null) offset = date - DateTimeOffset.Now;
                    if (offset != null)
                    {
                        if (offset > RetryDelay) offset = RetryDelay;
                        await Task.Delay(offset.Value, cancellationToken);
                        goto RETRY;
                    }
                }
            }
            response.EnsureSuccessStatusCode();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var jresp = await ProcessResponseAsync(response, cancellationToken);
                CheckErrors(jresp);
                return jresp;
            }
            catch (JsonReaderException)
            {
                // Input is not a valid json.
                Logger?.Warn($"Received non-json content: {request.RequestUri}");
                if (!allowsRetry || retries >= MaxRetries) throw;
                goto RETRY;
            }
        }

        private async Task<JToken> ProcessResponseAsync(HttpResponseMessage webResponse,
            CancellationToken cancellationToken)
        {
            using (webResponse)
            {
                string content;
                using (var s = await webResponse.Content.ReadAsStreamAsync())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    content = await s.ReadAllStringAsync(cancellationToken);
                }
                //Logger?.Trace(content);
                var obj = JToken.Parse(content);
                return obj;
            }
        }

        private void CheckErrors(JToken jresponse)
        {
            var obj = jresponse as JObject;
            if (obj == null) return;
            // See https://www.mediawiki.org/wiki/API:Errors_and_warnings .
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
                    case "readapidenied":       // You need read permission to use this module
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
