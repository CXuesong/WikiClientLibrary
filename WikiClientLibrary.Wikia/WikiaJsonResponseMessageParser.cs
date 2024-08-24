using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Wikia;

/// <summary>
/// Parser that parses the JSON and dispatches error in the response from generic Wikia API response.
/// </summary>
public class WikiaJsonResponseParser : WikiResponseMessageParser<JsonNode>
{

    internal static readonly WikiaJsonResponseParser Default = new WikiaJsonResponseParser();

    /// <inheritdoc />
    /// <remarks>
    /// <para>This method does not check the HTTP status code, because for certain JSON responses,
    /// the status code might has its semantic meanings.</para>
    /// <para>Then the content will be parsed as JSON, in <see cref="JsonNode"/>. If there is
    /// <see cref="JsonException"/> thrown while parsing the response, a retry will be requested.</para>
    /// <para>The default implementation for this method throws a <see cref="WikiaApiException"/>
    /// or one of its derived exceptions when detected <c>exception</c> node in the JSON response.
    /// The exception mapping is as follows</para>
    /// <list type="table">
    /// <listheader>
    /// <term><c>exception.code</c> value</term>
    /// <description>Mapped exception type</description>
    /// </listheader>
    /// <item>
    /// <term><c>NotFoundApiException</c></term>
    /// <description><see cref="NotFoundApiException"/></description>
    /// </item>
    /// <item>
    /// <term>Others</term>
    /// <description><see cref="WikiaApiException"/></description>
    /// </item>
    /// </list> 
    /// </remarks>
    public override async Task<JsonNode> ParseResponseAsync(HttpResponseMessage response, WikiResponseParsingContext context)
    {
        if (response == null) throw new ArgumentNullException(nameof(response));
        if (context == null) throw new ArgumentNullException(nameof(context));
        // For REST-ful API, we need to parse the content first, to see what happened.
        JsonNode jroot;
        try
        {
            jroot = await MediaWikiHelper.ParseJsonAsync(await response.Content.ReadAsStreamAsync(), context.CancellationToken);
        }
        catch (JsonException)
        {
            context.NeedRetry = true;
            throw;
        }
        if (jroot is JsonObject obj)
        {
            // Check for exception node.
            var exception = obj["exception"];
            if (exception != null)
            {
                var type = (string)exception["type"];
                var message = (string)exception["message"];
                var details = (string)exception["details"];
                var code = (int?)exception["code"] ?? (int)response.StatusCode;
                var traceId = (string)jroot["trace_id"];
                throw type switch
                {
                    "NotFoundApiException" => new NotFoundApiException(type, message, code, details, traceId),
                    _ => new WikiaApiException(type, message, code, details, traceId)
                };
            }
            // Check for exception node: {"status":404,"error":"ControllerNotFoundException","details":"Controller not found: ApiDocs"}
            {
                var status = (int?)obj["status"] ?? (int)response.StatusCode;
                var error = (string)obj["error"];
                if (status >= 400)
                {
                    var details = (string)obj["details"];
                    obj.Remove("status");
                    obj.Remove("error");
                    obj.Remove("details");
                    throw new WikiaApiException(error, details, status, obj.Count > 0 ? obj.ToString() : null, null);
                }
                if (error != null)
                {
                    context.Logger.LogWarning(
                        "Detected `error` node in the response, but status code does not signal any error. Status code: {Status}. Error: {Error}.",
                        status, (string)obj["error"]
                    );
                }
            }
        }
        return jroot;
    }

}
