using Newtonsoft.Json.Linq;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Pages.Queries.Properties;

/// <summary>
/// Returns plain-text or limited HTML extracts of the given pages.
/// <c>action=query&amp;prop=extracts</c>
/// (<a href="https://www.mediawiki.org/wiki/Extension:TextExtracts#API">mw:Extension:TextExtracts#API</a>)
/// </summary>
public class ExtractsPropertyProvider : WikiPagePropertyProvider<ExtractsPropertyGroup>
{

    /// <summary>
    /// How many characters to return. Actual text returned might be slightly longer.
    /// </summary>
    /// <value>The allowed maximum number of characters to return, or 0 for no such limitation.</value>
    /// <remarks>
    /// The effective value must be between 1 and 1,200.
    /// Either this property or <see cref="MaxSentences"/> should be 0.
    /// </remarks>
    public int MaxCharacters { get; set; }

    /// <summary>
    /// How many sentences to return.
    /// </summary>
    /// <value>The allowed maximum number of sentences to return, or 0 for no such limitation.</value>
    /// <remarks>
    /// The effective value must be between 1 and 10.
    /// Either this property or <see cref="MaxCharacters"/> should be 0.
    /// </remarks>
    public int MaxSentences { get; set; }

    /// <summary>
    /// Return only content before the first section.
    /// </summary>
    public bool IntroductionOnly { get; set; }

    /// <summary>
    /// Return extracts as plain text instead of limited HTML.
    /// </summary>
    public bool AsPlainText { get; set; }

    /// <inheritdoc />
    public override IEnumerable<KeyValuePair<string, object?>> EnumParameters(MediaWikiVersion version)
    {
        var p = new OrderedKeyValuePairs<string, object?>
        {
            { "exlimit", "max" }, { "exintro", IntroductionOnly }, { "exsectionformat", "plain" }, { "explaintext", AsPlainText },
        };
        if (MaxCharacters > 0) p.Add("exchars", MaxCharacters);
        if (MaxSentences > 0) p.Add("exsentences", MaxSentences);
        return p;
    }

    /// <inheritdoc />
    public override int GetMaxPaginationSize(MediaWikiVersion version, bool apiHighLimits)
    {
        return apiHighLimits ? 20 : 10;
    }

    /// <inheritdoc />
    public override string? PropertyName => "extracts";

    /// <inheritdoc />
    public override ExtractsPropertyGroup? ParsePropertyGroup(JObject json)
    {
        if (json == null) throw new ArgumentNullException(nameof(json));
        var jextract = json["extract"];
        if (jextract == null) return null;
        return ExtractsPropertyGroup.Create((string)jextract);
    }

}

public class ExtractsPropertyGroup : WikiPagePropertyGroup
{

    private static readonly ExtractsPropertyGroup Empty = new ExtractsPropertyGroup("");

    internal static ExtractsPropertyGroup Create(string? extract)
    {
        if (string.IsNullOrEmpty(extract)) return Empty;
        return new ExtractsPropertyGroup(extract);
    }

    private ExtractsPropertyGroup(string extract)
    {
        Extract = extract;
    }

    /// <summary>Extract of the page.</summary>
    public string Extract { get; }

}
