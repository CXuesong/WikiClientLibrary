using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Wikia
{
    public class WikiaJsonResonseParser : WikiResponseMessageParser<JToken>
    {

        internal static readonly WikiaJsonResonseParser Default = new WikiaJsonResonseParser();

        /// <inheritdoc />
        public override async Task<JToken> ParseResponseAsync(HttpResponseMessage response, WikiResponseParsingContext context)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));
            if (context == null) throw new ArgumentNullException(nameof(context));
            // For REST-ful API, we need to parse the content first, to see what happened.
            JToken jroot;
            try
            {
                jroot = await MediaWikiHelper.ParseJsonAsync(await response.Content.ReadAsStreamAsync(),
                    context.CancellationToken);
            }
            catch (JsonException)
            {
                context.NeedRetry = true;
                throw;
            }
            if (jroot is JObject obj)
            {
                // Check for exception node.
                var exception = obj["exception"];
                if (exception != null)
                {
                    var type = (string)exception["type"];
                    var message = (string)exception["message"];
                    var code = (int?)exception["code"] ?? (int)response.StatusCode;
                    var traceId = (string)jroot["trace_id"];
                    throw new WikiaApiException(type, message, code, traceId);
                }
            }
            return jroot;
        }
    }
}
