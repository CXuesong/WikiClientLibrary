using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using WikiClientLibrary.Pages;

namespace WikiClientLibrary
{
    /// <summary>
    /// Base exception class for WikiClientLibrary.
    /// </summary>
    public class WikiClientException : Exception
    {
        public WikiClientException() : base("An error has occurred performing MediaWiki operation.")
        {

        }

        public WikiClientException(string message) : base(message)
        {

        }

        public WikiClientException(string message, Exception innerException) : base(message, innerException)
        {
            
        }
    }

    /// <summary>
    /// An exception indicating the requested operation has failed.
    /// </summary>
    public class OperationFailedException : WikiClientException
    {
        /// <summary>
        /// Error code provided by MediaWiki API.
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// Detailed error message provided by MediaWiki API.
        /// </summary>
        public string ErrorMessage { get; }

        public OperationFailedException()
            : this("The requested operation has failed.")
        { }

        public OperationFailedException(string errorCode, string errorMessage)
            : base(
                string.Format(
                    string.IsNullOrEmpty(errorMessage)
                        ? "{0}"
                        : "{0}:{1}",
                    errorCode, errorMessage))
        {
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public OperationFailedException(string message)
            : this(null, message)
        {
            ErrorMessage = message;
        }

        public OperationFailedException(string message, Exception inner)
            : base(message, inner)
        {
            ErrorMessage = message;
        }
    }

    /// <summary>
    /// An exception indicating the requested action is invalid.
    /// </summary>
    public class InvalidActionException : OperationFailedException
    {
        public InvalidActionException(string errorCode, string message)
            : base(errorCode, message)
        {
        }
    }


    /// <summary>
    /// Raises when the account assertion fails when performing MediaWiki
    /// API requests.
    /// </summary>
    /// <remarks>See https://www.mediawiki.org/wiki/API:Assert .</remarks>
    public class AccountAssertionFailureException : OperationFailedException
    {
        public AccountAssertionFailureException(string errorCode, string message)
            : base(errorCode, message)
        { }
    }

    /// <summary>
    /// Raises when user has no rights for certain operations.
    /// </summary>
    public class UnauthorizedOperationException : OperationFailedException
    {
        public UnauthorizedOperationException(string errorCode, string message)
            : base(errorCode, message)
        {
        }

        public UnauthorizedOperationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Raises when conflict detected performing the operation.
    /// </summary>
    public class OperationConflictException : OperationFailedException
    {
        public OperationConflictException(string errorCode, string message)
            : base(errorCode, message)
        {
        }
    }

    /// <summary>
    /// An exception indicating the upload operation has at least one warning.
    /// </summary>
    public class UploadException : WikiClientException
    {
        /// <summary>
        /// The upload result that caused the exception.
        /// </summary>
        public UploadResult UploadResult { get; }

        private static string FormatMessage(UploadResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (result.Warnings.Count == 0)
                return $"An exception occured when trying to upload. Result code is {result.ResultCode}.";
            return string.Join(" ", result.Warnings.Select(p => UploadResult.FormatWarning(p.Key, p.Value)));
        }

        public UploadException(string message, Exception innerException) : base(message, innerException)
        { }

        public UploadException(UploadResult uploadResult) : base(FormatMessage(uploadResult))
        {
            UploadResult = uploadResult;
            if (uploadResult == null) throw new ArgumentNullException(nameof(uploadResult));
        }
    }

    /// <summary>
    /// Raises when the received network data is out of expectation.
    /// This may indicate the client library code is out of date.
    /// </summary>
    public class UnexpectedDataException : WikiClientException
    {
        public UnexpectedDataException()
            : this("Unexpected data received.")
        { }

        public UnexpectedDataException(string message)
            : base(message)
        { }

        public UnexpectedDataException(string message, Exception inner)
            : base(message, inner)
        { }
    }
}
