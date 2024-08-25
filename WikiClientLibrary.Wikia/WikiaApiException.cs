namespace WikiClientLibrary.Wikia;

/// <summary>
/// An exception that raises when received <c>exception</c> node
/// in JSON response from Wikia API requests.
/// </summary>
public class WikiaApiException : WikiClientException
{

    /// <summary>Wikia Exception type.</summary>
    public string ErrorType { get; }

    /// <summary>Wikia Exception message.</summary>
    public string ErrorMessage { get; }

    /// <summary>Wikia Exception code.</summary>
    public int ErrorCode { get; }

    /// <summary>Wikia Exception details.</summary>
    public string ErrorDetails { get; }

    /// <summary>Wikia error trace ID.</summary>
    public string TraceId { get; }

    public WikiaApiException()
        : this("The Wikia API invocation has failed.")
    {
    }

    public WikiaApiException(string errorType, string errorMessage, int errorCode, string errorDetails, string traceId)
        : this(null, errorType, errorMessage, errorCode, errorDetails, traceId)
    {
    }

    public WikiaApiException(string message, string errorType, string errorMessage, int errorCode,
        string errorDetails, string traceId)
    {
        if (message != null)
            Message = message;
        else if (errorDetails != null)
            Message = $"{errorType}:{errorMessage} ({errorCode}); {errorDetails}";
        else
            Message = $"{errorType}:{errorMessage} ({errorCode})";
        ErrorType = errorType;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
        ErrorDetails = errorDetails;
        TraceId = traceId;
    }

    public WikiaApiException(string message) : this(message, null, null, 0, null, null)
    {
    }

    /// <inheritdoc />
    public override string Message { get; }

}

/// <summary>
/// The CLR counterpart for Wikia <c>NotFoundException</c> (previously, <c>NotFoundApiException</c>).
/// </summary>
public class NotFoundApiException : WikiaApiException
{

    public NotFoundApiException() : base()
    {
    }

    public NotFoundApiException(string errorType, string errorMessage, int errorCode,
        string errorDetails, string traceId)
        : base(errorType, errorMessage, errorCode, errorDetails, traceId)
    {
    }

}
