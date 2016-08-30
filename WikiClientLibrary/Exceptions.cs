using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace WikiClientLibrary
{
    /// <summary>
    /// An exception indicating the requested operation has failed.
    /// </summary>
    public class OperationFailedException : InvalidOperationException
    {
        public string ErrorCode { get; }

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
            : base(message)
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
    /// Raises when user has no rights for certain operations.
    /// </summary>
    public class UnauthorizedOperationException : Exception
    {
        public UnauthorizedOperationException(string message)
            : base(message)
        {
        }

        public UnauthorizedOperationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public UnauthorizedOperationException(OperationFailedException innerException)
            : base(innerException?.Message, innerException)
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
    public class UploadException : Exception
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
    public class UnexpectedDataException : InvalidOperationException
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
