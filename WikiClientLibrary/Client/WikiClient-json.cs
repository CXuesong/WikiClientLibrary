using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
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

        /// <inheritdoc />
        private async Task<JToken> SendAsync(Func<HttpRequestMessage> requestFactory,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response;
            var retries = -1; // Current retries count.
            var request = requestFactory();
            if (request == null)
            {
                throw new ArgumentException(
                    "requestFactory should return an HttpRequestMessage instance for the 1st invocation.",
                    nameof(requestFactory));
            }
            RETRY:
            cancellationToken.ThrowIfCancellationRequested();
            retries++;
            if (retries > 0)
                Logger?.Trace(this, $"Retry x{retries}: {request.RequestUri}");
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
                    Logger?.Warn(this, $"Cancelled: {request.RequestUri}");
                    throw new OperationCanceledException();
                }
                else
                {
                    Logger?.Warn(this, $"Timeout: {request.RequestUri}");
                }
                request = requestFactory();
                if (request == null || retries >= MaxRetries) throw new TimeoutException();
                await Task.Delay(RetryDelay, cancellationToken);
                goto RETRY;
            }
            // Validate response.
            Logger?.Trace(this, $"{response.StatusCode}: {request.RequestUri}");
            var statusCode = (int) response.StatusCode;
            if (statusCode >= 500 && statusCode <= 599)
            {
                // Service Error. We can retry.
                // HTTP 503 : https://www.mediawiki.org/wiki/Manual:Maxlag_parameter
                request = requestFactory();
                if (request != null && retries < MaxRetries)
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
            Debug.Assert(request != null); // For HTTP 500~599, EnsureSuccessStatusCode will throw an Exception.
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
                Logger?.Warn(this, $"Received non-json content: {request.RequestUri}");
                request = requestFactory();
                if (request == null || retries >= MaxRetries) throw;
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
            /*
    "warnings": {
        "main": {
            "*": "xxxx"
        },
        "login": {
            "*": "xxxx"
        }
    }
             */
            if (jresponse["warnings"] != null)
            {
                if (Logger != null)
                {
                    var sb = new StringBuilder();
                    foreach (var module in ((JObject) jresponse["warnings"]).Properties())
                    {
                        if (sb.Length > 0) sb.AppendLine();
                        sb.Append(module.Name);
                        sb.Append(": ");
                        sb.Append(module.Value["*"] ?? module.Value);
                    }
                    Logger.Warn(this, sb.ToString());
                }
            }
            if (jresponse["error"] != null)
            {
                var err = jresponse["error"];
                var errcode = (string) err["code"];
                // err["*"]: API usage.
                var errmessage = ((string) err["info"]).Trim();
                switch (errcode)
                {
                    case "permissiondenied":
                    case "readapidenied": // You need read permission to use this module
                        throw new UnauthorizedOperationException(errmessage);
                    case "unknown_action":
                        throw new InvalidActionException(errcode, errmessage);
                    case "assertuserfailed":
                    case "assertbotfailed":
                        throw new AccountAssertionFailureException(errcode, errmessage);
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
