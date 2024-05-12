namespace WikiClientLibrary.Scribunto;

/// <summary>
/// Represents an error response from Scribunto Lua console.
/// </summary>
public class ScribuntoConsoleException : WikiClientException
{

    private static string MakeMessage(string? errorCode, string? errorMessage)
    {
        var message = "Error while evaluating expression";
        if (!string.IsNullOrEmpty(errorCode))
            message += ": " + errorCode;
        message += ".";
        if (!string.IsNullOrEmpty(errorMessage))
            message += " " + errorMessage;
        return message;
    }

    public ScribuntoConsoleException(string? errorCode, string? errorMessage)
        : this(errorCode, errorMessage, null, null)
    {
    }

    public ScribuntoConsoleException(string? errorCode, string? errorMessage, ScribuntoEvaluationResult? evaluationResult)
        : this(errorCode, errorMessage, evaluationResult, null)
    {
    }

    public ScribuntoConsoleException(string? errorCode, string? errorMessage, ScribuntoEvaluationResult? evaluationResult, string? message)
        : base(message ?? MakeMessage(errorCode, errorMessage))
    {
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        EvaluationResult = evaluationResult;
    }

    public ScribuntoConsoleException(string? message)
        : this(null, null, null, message)
    {
    }

    public ScribuntoConsoleException()
        : this(null, null, null, null)
    {
    }

    /// <summary>
    /// The evaluation result that causes the exception.
    /// </summary>
    public ScribuntoEvaluationResult? EvaluationResult { get; }

    /// <summary>
    /// The error code (<c>messagename</c>) received from server.
    /// </summary>
    /// <remarks>
    /// See <see cref="ScribuntoLuaErrorCodes" /> for a list of predefined error codes.
    /// </remarks>
    public string? ErrorCode { get; }

    /// <summary>
    /// The error description (<c>message</c>) received from server.
    /// </summary>
    public string? ErrorMessage { get; }

}

/// <summary>
/// A list of predefined Scribunto Lua error codes.
/// </summary>
/// <remarks>
/// See <a href="https://github.com/wikimedia/mediawiki-extensions-Scribunto/blob/master/i18n/en.json">this translation table</a>
/// for a list of possible error codes.
/// </remarks>
public static class ScribuntoLuaErrorCodes
{

    /// <summary>Script error: No such module '$2'.</summary> 
    public const string NoSuchModule = "scribunto-common-nosuchmodule";

    /// <summary>Script error: You must specify a function to call.</summary> 
    public const string NoFunction = "scribunto-common-nofunction";

    /// <summary>Script error: The function '$2' does not exist.</summary> 
    public const string NoSuchFunction = "scribunto-common-nosuchfunction";

    /// <summary>Script error: '$2' is not a function.</summary> 
    public const string NotAFunction = "scribunto-common-notafunction";

    /// <summary>The time allocated for running scripts has expired.</summary> 
    public const string EvaluationTimeout = "scribunto-common-timeout";

    /// <summary>The amount of memory allowed for running scripts has been exceeded.</summary> 
    public const string EvaluationOutOfMemory = "scribunto-common-oom";

}
