using HtmlAgilityPack;
using WikiClientLibrary.Client;

namespace WikiClientLibrary.Wikia;

public class WikiaHtmlResponseParser : WikiResponseMessageParser<HtmlDocument>
{

    internal static readonly WikiaHtmlResponseParser Default = new WikiaHtmlResponseParser();

    /// <inheritdoc />
    public override async Task<HtmlDocument> ParseResponseAsync(HttpResponseMessage response, WikiResponseParsingContext context)
    {
        if (!response.IsSuccessStatusCode)
        {
            context.NeedRetry = true;
            response.EnsureSuccessStatusCode();
        }
        var doc = new HtmlDocument();
        // TODO buffer stream, instead of reading all
        var content = await response.Content.ReadAsStringAsync();
        doc.LoadHtml(content);
        return doc;
    }

}
