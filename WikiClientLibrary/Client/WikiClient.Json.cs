using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WikiClientLibrary.Client
{
    partial class WikiClient
    {
        #region the json client

        /// <inheritdoc />
        private async Task<JToken> SendAsync(string endPointUrl, WikiRequestMessage message,
            CancellationToken cancellationToken)
        {
            Debug.Assert(message != null);
            HttpResponseMessage response;
            var retries = 0;

            Logger.LogTrace("Initiate request: {Request}, EndPoint: {EndPointUrl}.", message, endPointUrl);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, endPointUrl)
                {Content = message.GetHttpContent()};

            async Task<bool> PrepareForRetry(TimeSpan delay)
            {
                if (retries >= MaxRetries) return false;
                retries++;
                try
                {
                    httpRequest = new HttpRequestMessage(HttpMethod.Post, endPointUrl)
                        {Content = message.GetHttpContent()};
                }
                catch (Exception ex) when (ex is ObjectDisposedException || ex is InvalidOperationException)
                {
                    // Some content (e.g. StreamContent with un-seekable Stream) may throw this exception
                    // on the second try.
                    Logger.LogWarning("Cannot retry: {Request}. {Exception}.", message, ex.Message);
                    return false;
                }
                Logger.LogDebug("Retry: {Request} after {Delay}, attempt #{Retries}.", message, RetryDelay, retries);
                await Task.Delay(delay, cancellationToken);
                return true;
            }

            RETRY:
            if (retries > 0)
                Logger.LogTrace("Initiate request: {Request}.", message, endPointUrl);
            cancellationToken.ThrowIfCancellationRequested();
            var requestSw = Stopwatch.StartNew();
            try
            {
                // Use await instead of responseTask.Result to unwrap Exceptions.
                // Or AggregateException might be thrown.
                using (var responseCancellation = new CancellationTokenSource(Timeout))
                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, responseCancellation.Token))
                    response = await HttpClient.SendAsync(httpRequest, linkedCts.Token);
                // The request has been finished.
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Logger.LogWarning("Cancelled via cancellationToken: {Request}", message);
                    throw new OperationCanceledException();
                }
                Logger.LogWarning("Timeout: {Request}", message);
                if (!await PrepareForRetry(RetryDelay)) throw new TimeoutException();
                goto RETRY;
            }
            // Validate response.
            var statusCode = (int) response.StatusCode;
            Logger.LogTrace("HTTP {StatusCode}: {Request}, elapsed: {Time}",
                statusCode, message, requestSw.Elapsed);
            if (!response.IsSuccessStatusCode)
            {
                Logger.LogWarning("HTTP {StatusCode} {Reason}: {Request}.",
                    statusCode, response.ReasonPhrase, message, requestSw.Elapsed);
            }
            if (statusCode >= 500 && statusCode <= 599)
            {
                // Service Error. We can retry.
                // HTTP 503 : https://www.mediawiki.org/wiki/Manual:Maxlag_parameter
                if (retries < MaxRetries)
                {
                    // Delay per Retry-After Header
                    var date = response.Headers.RetryAfter?.Date;
                    var delay = response.Headers.RetryAfter?.Delta;
                    if (delay == null && date != null) delay = date - DateTimeOffset.Now;
                    // Or use the default delay
                    if (delay == null) delay = RetryDelay;
                    else if (delay > RetryDelay) delay = RetryDelay;
                    if (await PrepareForRetry(delay.Value)) goto RETRY;
                }
            }
            // For HTTP 500~599, EnsureSuccessStatusCode will throw an Exception.
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
                Logger.LogWarning("Received non-json content: {Uri}", httpRequest.RequestUri);
                if (httpRequest == null || retries >= MaxRetries) throw;
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
            if (jresponse["warnings"] != null && Logger.IsEnabled(LogLevel.Debug))
            {
                foreach (var module in ((JObject) jresponse["warnings"]).Properties())
                {
                    Logger.LogWarning("{Module}: {Warning}", module.Name, module.Value);
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
                    case "mustbeloggedin": // You must be logged in to upload this file.
                        throw new UnauthorizedOperationException(errcode, errmessage);
                    case "badtoken":
                        throw new BadTokenException(errcode, errmessage);
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
