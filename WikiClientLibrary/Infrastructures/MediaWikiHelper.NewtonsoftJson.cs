using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace WikiClientLibrary.Infrastructures;

partial class MediaWikiHelper
{

    /// <summary>
    /// Create an new instance of <see cref="JsonSerializer"/> for parsing MediaWiki API response.
    /// </summary>
    [Obsolete("Newtonsoft.Json API is being deprecated in favor of System.Text.Json API.")]
    public static JsonSerializer CreateWikiJsonSerializer()
    {
        return Utility.CreateWikiJsonSerializer();
    }

    // Disable automatic datetime detection for JValue.
    // We are converting string to JObject, then from JObject to data model.
    // If date time is already converted into JValue of type Date,
    // we won't be easily recover the underlying string when converting to data model.
    private static readonly JsonSerializer jTokenSerializer = new JsonSerializer { DateParseHandling = DateParseHandling.None };

    // TODO Remove this member before release
    /// <summary>
    /// Asynchronously parses JSON content from the specified stream.
    /// </summary>
    /// <param name="stream">The stream containing the non-empty JSON content.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <c>null</c>.</exception>
    /// <exception cref="UnexpectedDataException"><paramref name="stream"/> is empty stream, or there is an error parsing the JSON response.</exception>
    [Obsolete("Newtonsoft.Json API is being deprecated in favor of System.Text.Json API.")]
    public static async Task<JToken> ParseJsonAsync0(Stream stream, CancellationToken cancellationToken)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        // TODO buffer stream, instead of reading all
        var content = await stream.ReadAllStringAsync(cancellationToken);
        if (string.IsNullOrEmpty(content))
            throw new UnexpectedDataException(Prompts.ExceptionJsonEmptyInput);
        if (content[0] == '<')
            throw new UnexpectedDataException(Prompts.ExceptionHtmlResponseHint) { HelpLink = ExceptionTroubleshootingHelpLink };
        try
        {
            using var reader = new StringReader(content);
            using var jreader = new JsonTextReader(reader);
            return jTokenSerializer.Deserialize<JToken>(jreader);
        }
        catch (Exception ex)
        {
            var message = Prompts.ExceptionJsonParsingFailed;
            message += ex.Message;
            if (ex is JsonException && !string.IsNullOrEmpty(ex.Message) && ex.Message.Contains("'<'"))
                message += Prompts.ExceptionJsonEmptyInput;
            throw new UnexpectedDataException(message, ex);
        }
    }
}
