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
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Client
{
    /// <summary>
    /// Provides basic operations for MediaWiki API.
    /// </summary>
    public abstract class WikiClientBase : IWikiClientLoggable, IDisposable
    {

        private int _MaxRetries = 3;
        private ILoggerFactory _LoggerFactory = null;

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
        /// Invokes MediaWiki API and gets JSON result.
        /// </summary>
        /// <param name="endPointUrl">The API endpoint URL.</param>
        /// <param name="message">The request message.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <exception cref="InvalidActionException">Specified action is not supported.</exception>
        /// <exception cref="UnauthorizedOperationException">Permission denied.</exception>
        /// <exception cref="OperationFailedException">There is "error" node in returned JSON.</exception>
        public abstract Task<JToken> GetJsonAsync(string endPointUrl, WikiRequestMessage message,
            CancellationToken cancellationToken);

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
        public ILoggerFactory LoggerFactory
        {
            get => _LoggerFactory;
            set => Logger = Utility.SetLoggerFactory(ref _LoggerFactory, value, GetType());
        }

        protected ILogger Logger { get; private set; } = NullLogger.Instance;
    }
}