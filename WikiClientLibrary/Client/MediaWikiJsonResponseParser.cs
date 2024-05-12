using System.Globalization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Client;

/// <summary>
/// Parser that parses the JSON and dispatches error in the response from MediaWiki API response.
/// </summary>
public class MediaWikiJsonResponseParser : WikiResponseMessageParser<JToken>
{

    /// <summary>Gets a default instance of <see cref="MediaWikiJsonResponseParser"/>.</summary>
    public static MediaWikiJsonResponseParser Default { get; } = new MediaWikiJsonResponseParser();

    /// <inheritdoc />
    /// <remarks>
    /// <para>This method checks the HTTP status code first.
    /// For non-successful HTTP status codes, this method will request for a retry.</para>
    /// <para>Then the content will be parsed as JSON, in <see cref="JToken"/>. If there is
    /// <see cref="JsonException"/> thrown while parsing the response, a retry will be requested.</para>
    /// <para>Finally, before returning the parsed JSON, this method checks for <c>warning</c> and <c>error</c>
    /// nodes. If there exists <c>warning</c> node, a warning will be issued to the logger. If there exists <c>error</c>
    /// node, a <see cref="OperationFailedException"/> or its derived exception will be thrown. You can
    /// customize the error generation behavior by overriding <see cref="OnApiError"/> method.</para>
    /// </remarks>
    public override async Task<JToken> ParseResponseAsync(HttpResponseMessage response, WikiResponseParsingContext context)
    {
        if (response == null) throw new ArgumentNullException(nameof(response));
        if (context == null) throw new ArgumentNullException(nameof(context));
        // Check response status first.
        if (!response.IsSuccessStatusCode)
        {
            context.NeedRetry = true;
            response.EnsureSuccessStatusCode();
        }
        JToken jroot;
        try
        {
            await using var s = await response.Content.ReadAsStreamAsync();
            jroot = await MediaWikiHelper.ParseJsonAsync(s, context.CancellationToken);
        }
        catch (JsonException)
        {
            // Input is not valid json.
            context.NeedRetry = true;
            throw;
        }
        // See https://www.mediawiki.org/wiki/API:Errors_and_warnings .
        /*
"warnings": {
    "main": {
        "*": "xxxx"
    },
    "login": {
        "*": "xxxx"
    }
}
         */
        // Note that in MW 1.19, action=logout returns [] instead of {}
        if (jroot is JObject jobj)
        {
            if (jobj["warnings"] != null && context.Logger.IsEnabled(LogLevel.Warning))
            {
                foreach (var module in ((JObject)jobj["warnings"]).Properties())
                {
                    context.Logger.LogWarning("API warning [{Module}]: {Warning}", module.Name, module.Value);
                }
            }
            var err = jobj["error"];
            if (err != null)
            {
                var errcode = (string)err["code"];
                // err["*"]: API usage.
                var errmessage = ((string)err["info"]).Trim();
                context.Logger.LogWarning("API error: {Code} - {Message}", errcode, errmessage);
                OnApiError(errcode, errmessage, err, jobj, context);
            }
        }
        return jroot;
    }

    /// <summary>
    /// Called when <c>error</c> node presents in the API response.
    /// </summary>
    /// <param name="errorCode">Error code. (<c>error.code</c>)</param>
    /// <param name="errorMessage">Error message. (<c>error.info</c>)</param>
    /// <param name="errorNode">The <c>error</c> JSON node.</param>
    /// <param name="responseNode">The JSON root of the API response.</param>
    /// <param name="context">The response parsing context, used for initiating a retry.</param>
    /// <remarks>
    /// <para>The default implementation for this method throws a <see cref="OperationFailedException"/>
    /// or one of its derived exceptions. The default exception mapping is as follows</para>
    /// <list type="table">
    /// <listheader>
    /// <term><paramref name="errorCode"/> value</term>
    /// <description>Mapped exception type</description>
    /// </listheader>
    /// <item>
    /// <term><c>maxlag</c></term>
    /// <description><see cref="ServerLagException"/>; <see cref="WikiResponseParsingContext.NeedRetry"/> will be set to <c>true</c>.</description>
    /// </item>
    /// <item>
    /// <term><c>permissiondenied</c>, <c>readapidenied</c>, <c>mustbeloggedin</c></term>
    /// <description><see cref="UnauthorizedOperationException"/></description>
    /// </item>
    /// <item>
    /// <term><c>permissions</c> (Flow)</term>
    /// <description><see cref="UnauthorizedOperationException"/></description>
    /// </item>
    /// <item>
    /// <term><c>badtoken</c></term>
    /// <description><see cref="BadTokenException"/></description>
    /// </item>
    /// <item>
    /// <term><c>unknown_action</c></term>
    /// <description><see cref="InvalidActionException"/></description>
    /// </item>
    /// <item>
    /// <term><c>assertuserfailed</c>, <c>assertbotfailed</c></term>
    /// <description><see cref="AccountAssertionFailureException"/></description>
    /// </item>
    /// <item>
    /// <term><c>*conflict</c></term>
    /// <description><see cref="OperationConflictException"/></description>
    /// </item>
    /// <item>
    /// <term><c>prev_revision</c> (Flow)</term>
    /// <description><see cref="OperationConflictException"/></description>
    /// </item>
    /// <item>
    /// <item>
    /// <term><c>internal_api_error*</c></term>
    /// <description><see cref="MediaWikiRemoteException"/></description>
    /// </item>
    /// <term>others</term>
    /// <description><see cref="OperationFailedException"/></description>
    /// </item>
    /// </list> 
    /// </remarks>
    protected virtual void OnApiError(string errorCode, string errorMessage,
        JToken errorNode, JToken responseNode, WikiResponseParsingContext context)
    {
        var fullMessage = errorMessage;
        // Append additional messages from WMF, if any.
        var jmessages = (JArray?)errorNode["messages"];
        if (jmessages != null && jmessages.Count > 1 && jmessages[0]["html"] != null)
        {
            // jmessages[0] usually is the same as errorMessage
            fullMessage = string.Join(" ", jmessages.Select(m => (string)m["html"]["*"]));
        }
        switch (errorCode)
        {
            case "maxlag":  // maxlag reached.
                context.NeedRetry = true;
                throw new ServerLagException(errorCode, fullMessage, (double?)errorNode["lag"] ?? 0, (string)errorNode["type"], (string)errorNode["host"]);
            case "permissiondenied":
            case "readapidenied": // You need read permission to use this module.
            case "mustbeloggedin": // You must be logged in to upload this file.
                throw new UnauthorizedOperationException(errorCode, fullMessage);
            case "permissions":
                JToken jPermissions;
                if ((jPermissions = errorNode["permissions"]) != null)
                {
                    if (jPermissions.Type != JTokenType.Null)
                    {
                        var permissions = jPermissions is JArray a
                            ? a.Select(c => c is JValue v ? Convert.ToString(v.Value, CultureInfo.InvariantCulture) : c.ToString())
                            : new[]
                            {
                                jPermissions is JValue v1
                                    ? Convert.ToString(v1.Value, CultureInfo.InvariantCulture)
                                    : jPermissions.ToString()
                            };
                        throw new UnauthorizedOperationException(errorCode, fullMessage, permissions.ToList()!);
                    }
                }
                throw new UnauthorizedOperationException(errorCode, fullMessage);
            case "badtoken":
                throw new BadTokenException(errorCode, fullMessage);
            case "unknown_action":
                throw new InvalidActionException(errorCode, fullMessage);
            case "assertuserfailed":
            case "assertbotfailed":
                throw new AccountAssertionFailureException(errorCode, fullMessage);
            case "prev_revision":
                throw new OperationConflictException(errorCode, fullMessage);
            case "badvalue":    // since 1.35.0-wmf.19
                // throw more specific Exception, if possible.
                if (fullMessage.Contains("\"action\""))
                    throw new InvalidActionException(errorCode, fullMessage);
                throw new BadValueException(errorCode, fullMessage);
            default:
                if (errorCode.StartsWith("unknown_", StringComparison.OrdinalIgnoreCase))
                    throw new BadValueException(errorCode, fullMessage);
                if (errorCode.EndsWith("conflict", StringComparison.OrdinalIgnoreCase))
                    throw new OperationConflictException(errorCode, fullMessage);
                // "messages": [
                // {
                // "name": "wikibase-api-failed-save",
                // "parameters": [],
                // "html": {
                // "*": "The save has failed."
                // }
                // },
                // ...]
                if (errorCode.StartsWith("internal_api_error", StringComparison.OrdinalIgnoreCase))
                {
                    throw new MediaWikiRemoteException(errorCode, fullMessage,
                        (string)errorNode["errorclass"], (string)errorNode["*"]);
                }
                throw new OperationFailedException(errorCode, fullMessage);
        }
    }

}