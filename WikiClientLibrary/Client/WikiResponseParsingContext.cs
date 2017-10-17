using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace WikiClientLibrary.Client
{
    /// <summary>
    /// Provides parsing context for <see cref="IWikiResponseMessageParser.ParseResponseAsync"/>.
    /// </summary>
    public class WikiResponseParsingContext
    {

        /// <param name="logger">The logger.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <c>null</c>.</exception>
        public WikiResponseParsingContext(ILogger logger, CancellationToken cancellationToken)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// The logger.
        /// </summary>
        public ILogger Logger { get; }

        /// <summary>
        /// The token used to cancel the operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// When set in <see cref="IWikiResponseMessageParser.ParseResponseAsync"/> implementation,
        /// requests for retrying the request.
        /// </summary>
        /// <remarks>
        /// Normally after setting this property to <c>true</c>, a <c>return</c> or <c>throw</c> statement will follow.
        /// See <see cref="IWikiResponseMessageParser.ParseResponseAsync"/> for the detailed usage of this property.
        /// </remarks>
        public bool NeedRetry { get; set; }

    }
}
