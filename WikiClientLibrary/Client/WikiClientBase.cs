using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WikiClientLibrary.Client
{
    /// <summary>
    /// Provides basic operations for MediaWiki API.
    /// </summary>
    public abstract class WikiClientBase : IWikiClientLoggable, IDisposable
    {

        private int _MaxRetries = 3;
        internal ILoggerFactory loggerFactory = null;
        internal ILogger logger = NullLogger.Instance;
        
        /// <summary>
        /// Timeout for each query.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Delay before each retry.
        /// </summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Max retries count.
        /// </summary>
        public int MaxRetries
        {
            get { return _MaxRetries; }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                _MaxRetries = value;
            }
        }

        /// <summary>
        /// Invokes API and gets JSON result.
        /// </summary>
        /// <param name="endPointUrl">The API endpoint URL.</param>
        /// <param name="queryParams">The parameters of the query.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <exception cref="InvalidActionException">Specified action is not supported.</exception>
        /// <exception cref="UnauthorizedOperationException">Permission denied.</exception>
        /// <exception cref="OperationFailedException">There's "error" node in returned JSON.</exception>
        public abstract Task<JToken> GetJsonAsync(string endPointUrl,
            IEnumerable<KeyValuePair<string, string>> queryParams,
            CancellationToken cancellationToken);

        /// <summary>
        /// Invokes API and gets JSON result.
        /// </summary>
        /// <param name="endPointUrl">The API endpoint URL.</param>
        /// <param name="postContentFactory">The factory function that returns a new <see cref="HttpContent"/> per invocation.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <exception cref="ArgumentException"><paramref name="postContentFactory" /> returns <c>null</c> for the first invocation.</exception>
        /// <exception cref="InvalidActionException">Specified action is not supported.</exception>
        /// <exception cref="UnauthorizedOperationException">Permission denied.</exception>
        /// <exception cref="OperationFailedException">There's "error" node in returned JSON.</exception>
        /// <remarks>
        /// <para>"Get" means the returned value is JSON, though the request is sent via HTTP POST.</para>
        /// <para>If <paramref name="postContentFactory" /> returns <c>null</c> for the first invocation, an
        /// <see cref="ArgumentException"/> will be thrown. If it returns <c>null</c> for subsequent invocations
        /// (often when retrying the request), no further retry will be performed.</para>
        /// <para>You need to specify format=json manually in the request content.</para>
        /// </remarks>
        public abstract Task<JToken> GetJsonAsync(string endPointUrl, Func<HttpContent> postContentFactory, CancellationToken cancellationToken);

        /// <summary>
        /// Invokes API and gets JSON result.
        /// </summary>
        /// <param name="endPointUrl">The API endpoint URL.</param>
        /// <param name="queryParams">The parameters of the query, which will be converted into key-value pairs.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <exception cref="InvalidActionException">Specified action is not supported.</exception>
        /// <exception cref="OperationFailedException">There's "error" node in returned JSON.</exception>
        public virtual Task<JToken> GetJsonAsync(string endPointUrl, object queryParams, CancellationToken cancellationToken)
        {
            return GetJsonAsync(endPointUrl, Utility.ToWikiStringValuePairs(queryParams), cancellationToken);
        }

        /// <inheritdoc />
        protected virtual void Dispose(bool disposing)
        {
            // release unmanaged resources here
            if (disposing)
            {
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        ~WikiClientBase()
        {
            Dispose(false);
        }

        /// <inheritdoc />
        public void SetLoggerFactory(ILoggerFactory factory)
        {
            logger = factory == null ? (ILogger) NullLogger.Instance : factory.CreateLogger<WikiClient>();
            loggerFactory = factory;
        }

    }
}