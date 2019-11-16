using System;
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
                        : "{0}: {1}",
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
    /// <remarks>
    /// This exception corresponds to the following MW API errors:
    /// <list type="bullet">
    /// <item>
    /// <term>readapidenied</term>
    /// <description>You need read permission to use this module.</description>
    /// </item>
    /// <item><term>permissiondenied</term></item>
    /// <item>
    /// <term>mustbeloggedin</term>
    /// <description>You must be logged in to upload this file.</description>
    /// </item>
    /// <item><term>readapidenied</term></item>
    /// <item><term>permissions</term></item>
    /// <item><term>cantpurge</term></item>
    /// <item>
    /// <term>(empty ErrorCode)</term>
    /// <description>Other cases where the user does not have the rights to perform the operation.</description>
    /// </item>
    /// </list>
    /// </remarks>
    public class UnauthorizedOperationException : OperationFailedException
    {

        private readonly string[] emptyStrings = { };

        public UnauthorizedOperationException(string errorCode, string message)
            : this(errorCode, message, null)
        {
        }

        public UnauthorizedOperationException(string errorCode, string message, IReadOnlyCollection<string> desiredPermissions)
            : base(errorCode, message)
        {
            DesiredPermissions = desiredPermissions ?? emptyStrings;
        }

        public UnauthorizedOperationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public UnauthorizedOperationException(OperationFailedException ex)
            : base(ex.ErrorCode, ex.ErrorMessage)
        {
        }

        /// <summary>
        /// The desired permission(s) to perform the operation.
        /// </summary>
        public IReadOnlyCollection<string> DesiredPermissions { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            var message = base.ToString();
            if (DesiredPermissions.Count > 0)
            {
                message += " " + string.Format(Prompts.ExceptionDesiredPermissions1, string.Join(",", DesiredPermissions));
            }
            return message;
        }
    }

    /// <summary>
    /// Raises when conflict detected performing the operation. (<c>*conflict</c>)
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
    /// Raises when the token used to invoke MediaWiki API is invalid. (<c>badtoken</c>)
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
    /// Raises when the remote MediaWiki site has encountered an internal error. (<c>internal_api_error_*</c>)
    /// </summary>
    /// <remarks>This corresponds to <c>internal_api_error_*</c> MW API error.</remarks>
    public class MediaWikiRemoteException : OperationFailedException
    {

        public virtual string ErrorClass { get; }

        public virtual string RemoteStackTrace { get; }

        public MediaWikiRemoteException(string errorCode, string message)
            : this(errorCode, message, null, null)
        {
        }

        public MediaWikiRemoteException(string errorCode, string message, string errorClass, string remoteStackTrace)
            : base(errorCode, message)
        {
            ErrorClass = errorClass;
            RemoteStackTrace = remoteStackTrace;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(GetType());
            sb.Append(": ");
            sb.Append(Message);
            sb.AppendLine();
            if (!string.IsNullOrEmpty(RemoteStackTrace))
            {
                sb.AppendLine(RemoteStackTrace);
                sb.AppendLine(Prompts.MediaWikiRemoteStackTraceEnd);
            }
            sb.Append(StackTrace);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Raises when MediaWiki server replication lag
    /// does not meet the required limitation.
    /// (<a href="https://www.mediawiki.org/wiki/Manual:Maxlag_parameter">mw:Manual:Maxlag parameter</a>)
    /// </summary>
    public class ServerLagException : OperationFailedException
    {

        public ServerLagException(string errorCode, string message, double lag, string lagType, string laggedHost)
            : base(errorCode, message)
        {
            Lag = lag;
            LaggedHost = laggedHost;
            LagType = lagType;
        }

        /// <summary>
        /// The actual lag in seconds.
        /// </summary>
        public double Lag { get; }

        /// <summary>
        /// Type of the lag.
        /// </summary>
        /// <remarks>
        /// For most of the cases, the value is <c>"db"</c>.
        /// </remarks>
        public string LagType { get; }

        /// <summary>
        /// The lagged host name.
        /// </summary>
        public string LaggedHost { get; }

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
