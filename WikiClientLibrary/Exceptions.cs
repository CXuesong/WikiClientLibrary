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
    /// Raises when user has no rights for certain operations.
    /// </summary>
    public class UnauthorizedOperationException : OperationFailedException
    {

        public UnauthorizedOperationException(string message)
            : base(message)
        {
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
