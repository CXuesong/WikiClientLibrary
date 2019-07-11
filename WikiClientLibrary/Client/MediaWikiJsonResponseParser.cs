using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Client
{

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
                using (var s = await response.Content.ReadAsStreamAsync())
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
        protected virtual void OnApiError(string errorCode, string errorMessage, JToken errorNode, JToken responseNode,
            WikiResponseParsingContext context)
        {
            switch (errorCode)
            {
                case "permissiondenied":
                case "readapidenied": // You need read permission to use this module
                case "mustbeloggedin": // You must be logged in to upload this file.
                    throw new UnauthorizedOperationException(errorCode, errorMessage);
                case "permissions":
                    if (errorNode["permissions"] != null && errorNode["permissions"].Type != JTokenType.Null)
                        errorMessage += " Desired permissions:" + errorNode["permissions"]?.ToString(Formatting.None);
                    throw new UnauthorizedOperationException(errorCode, errorMessage);
                case "badtoken":
                    throw new BadTokenException(errorCode, errorMessage);
                case "unknown_action":
                    throw new InvalidActionException(errorCode, errorMessage);
                case "assertuserfailed":
                case "assertbotfailed":
                    throw new AccountAssertionFailureException(errorCode, errorMessage);
                case "prev_revision":
                    throw new OperationConflictException(errorCode, errorMessage);
                default:
                    if (errorCode.EndsWith("conflict", StringComparison.OrdinalIgnoreCase))
                        throw new OperationConflictException(errorCode, errorMessage);
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
                        throw new MediaWikiRemoteException(errorCode, errorMessage, 
                            (string)errorNode["errorclass"], (string)errorNode["*"]);
                    }
                    var messages = (JArray)errorNode["messages"];
                    if (messages != null && messages.Count > 1 && messages[0]["html"] != null)
                    {
                        errorMessage = string.Join(" ", messages.Select(m => (string)m["html"]["*"]));
                    }
                    throw new OperationFailedException(errorCode, errorMessage);
            }
        }

    }
}