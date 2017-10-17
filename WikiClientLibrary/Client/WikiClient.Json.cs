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

        protected virtual HttpRequestMessage CreateHttpRequestMessage(string endPointUrl, WikiRequestMessage message)
        {
            var url = endPointUrl;
            var query = message.GetHttpQuery();
            if (query != null) url = url + "?" + query;
            return new HttpRequestMessage(message.GetHttpMethod(), url)
                {Content = message.GetHttpContent()};
        }

        private async Task<object> SendAsync(string endPointUrl, WikiRequestMessage message,
            IWikiResponseMessageParser responseParser, CancellationToken cancellationToken)
        {
            Debug.Assert(endPointUrl != null);
            Debug.Assert(message != null);

            var httpRequest = CreateHttpRequestMessage(endPointUrl, message);
            var retries = 0;

            async Task<bool> PrepareForRetry(TimeSpan delay)
            {
                if (retries >= MaxRetries) return false;
                retries++;
                try
                {
                    httpRequest = CreateHttpRequestMessage(endPointUrl, message);
                }
                catch (Exception ex) when (ex is ObjectDisposedException || ex is InvalidOperationException)
                {
                    // Some content (e.g. StreamContent with un-seekable Stream) may throw this exception
                    // on the second try.
                    Logger.LogWarning("Cannot retry: {Exception}.", ex.Message);
                    return false;
                }
                Logger.LogDebug("Retry #{Retries} after {Delay}.", retries, RetryDelay);
                await Task.Delay(delay, cancellationToken);
                return true;
            }

            RETRY:
            Logger.LogTrace("Initiate request to: {EndPointUrl}.", endPointUrl);
            cancellationToken.ThrowIfCancellationRequested();
            var requestSw = Stopwatch.StartNew();
            HttpResponseMessage response;
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
                    Logger.LogWarning("Cancelled via CancellationToken.");
                    cancellationToken.ThrowIfCancellationRequested();
                }
                Logger.LogWarning("Timeout.");
                if (!await PrepareForRetry(RetryDelay)) throw new TimeoutException();
                goto RETRY;
            }
            using (response)
            {
                // Validate response.
                var statusCode = (int)response.StatusCode;
                Logger.LogTrace("HTTP {StatusCode}, elapsed: {Time}", statusCode, requestSw.Elapsed);
                if (!response.IsSuccessStatusCode)
                    Logger.LogWarning("HTTP {StatusCode} {Reason}.", statusCode, response.ReasonPhrase, requestSw.Elapsed);
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
                cancellationToken.ThrowIfCancellationRequested();
                var context = new WikiResponseParsingContext(Logger, cancellationToken);
                try
                {
                    var parsed = await responseParser.ParseResponseAsync(response, context);
                    if (context.NeedRetry)
                    {
                        if (await PrepareForRetry(RetryDelay)) goto RETRY;
                        throw new InvalidOperationException("Reached maximum count of retries.");
                    }
                    return parsed;
                }
                catch (Exception ex)
                {
                    if (context.NeedRetry && await PrepareForRetry(RetryDelay))
                    {
                        Logger.LogWarning("{Parser}: {Message}", responseParser, ex.Message);
                        goto RETRY;
                    }
                    Logger.LogWarning(new EventId(), ex, "Parser {Parser} throws an Exception.", responseParser);
                    throw;
                }
            }
        }
    }
}