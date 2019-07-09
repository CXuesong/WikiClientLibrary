﻿using System;
using System.Collections.Generic;
using System.Linq;
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
        public WikiClientException() : base(Prompts.ExceptionWikiClientGeneral)
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
    /// <remarks>This often corresponds to MediaWiki API error, i.e. API responses with "error" node.</remarks>
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
            : this(Prompts.ExceptionRequestFailed)
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
    /// <remarks>This corresponds to <c>unknown_action</c> MW API error.</remarks>
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
    /// <remarks>
    /// This corresponds to <c>assertuserfailed</c> or <c>assertbotfailed</c> MW API error.
    /// <para>See <a href="https://www.mediawiki.org/wiki/API:Assert">mw:API:Assert</a>.</para>
    /// </remarks>
    public class AccountAssertionFailureException : OperationFailedException
    {
        public AccountAssertionFailureException(string errorCode, string message)
            : base(errorCode, message)
        { }
    }

    /// <summary>
    /// Raises when user has no rights for certain operations.
    /// </summary>
    /// <remarks>This corresponds to <c>*conflict</c> MW API error.</remarks>
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

        public UnauthorizedOperationException(OperationFailedException ex)
            : base(ex.ErrorCode, ex.ErrorMessage)
        {
        }

    }

    /// <summary>
    /// Raises when conflict detected performing the operation.
    /// </summary>
    /// <remarks>This corresponds to <c>*conflict</c> MW API error.</remarks>
    public class OperationConflictException : OperationFailedException
    {
        public OperationConflictException(string errorCode, string message)
            : base(errorCode, message)
        {
        }
    }

    /// <summary>
    /// Raises when the token used to invoke MediaWiki API is invalid.
    /// </summary>
    /// <remarks>This corresponds to <c>badtoken</c> MW API error.</remarks>
    public class BadTokenException : OperationFailedException
    {
        public BadTokenException(string errorCode, string message)
            : base(errorCode, message)
        {
        }
    }

    /// <summary>
    /// Raises when the received network data is out of expectation.
    /// This may indicate the client library code is out of date.
    /// </summary>
    public class UnexpectedDataException : WikiClientException
    {
        public UnexpectedDataException()
            : this(Prompts.ExceptionUnexpectedData)
        { }

        public UnexpectedDataException(string message)
            : base(message)
        { }

        public UnexpectedDataException(string message, Exception inner)
            : base(message, inner)
        { }
    }
}
