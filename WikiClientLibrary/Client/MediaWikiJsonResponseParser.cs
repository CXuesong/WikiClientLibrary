using System;
using System.Net.Http;
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
        /// <param name="errorMessage">Errir message. (<c>error.info</c>)</param>
        /// <param name="errorNode">The <c>error</c> JSON node.</param>
        /// <param name="responseNode">The JSON root of the API response.</param>
        /// <param name="context">The response parsing context, used for initiating a retry.</param>
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
                    if (errorCode.EndsWith("conflict"))
                        throw new OperationConflictException(errorCode, errorMessage);
                    throw new OperationFailedException(errorCode, errorMessage);
            }
        }

    }
}