using System;
using System.Collections.Generic;
using System.Text;

namespace WikiClientLibrary.Wikia
{

    /// <summary>
    /// An exception that raises when received <c>exception</c> node
    /// in JSON response from Wikia API requests.
    /// </summary>
    public class WikiaApiException : WikiClientException
    {

        private readonly string localMessage;

        /// <summary>
        /// Wikia Exception type.
        /// </summary>
        public string ErrorType { get; }

        /// <summary>
        /// Wikia Exception message.
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// Wikia Exception code.
        /// </summary>
        public int ErrorCode { get; }

        /// <summary>
        /// Wikia error trace ID.
        /// </summary>
        public string TraceId { get; }

        public WikiaApiException()
            : this("The Wikia API invocation has failed.")
        { }

        public WikiaApiException(string errorType, string errorMessage, int errorCode, string traceId)
            : this(null, errorType, errorMessage, errorCode, traceId)
        {
        }

        public WikiaApiException(string message, string errorType, string errorMessage, int errorCode, string traceId)
        {
            localMessage = message;
            ErrorType = errorType;
            ErrorMessage = errorMessage;
            ErrorCode = errorCode;
            TraceId = traceId;
        }

        public WikiaApiException(string message) : this(message, null, null, 0, null)
        {
        }

        /// <inheritdoc />
        public override string Message
        {
            get
            {
                if (localMessage != null) return localMessage;
                return $"{ErrorType}:{ErrorMessage} ({ErrorCode})";
            }
        }
    }
}
