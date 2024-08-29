using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages.Parsing;

namespace WikiClientLibrary.Pages.Queries.Properties;

/// <summary>
/// Gets a list of interlanguage links from the provided pages to other languages.
/// (<a href="https://www.mediawiki.org/wiki/API:Langlinks">mw:API:Langlinks</a>)
/// </summary>
public class LanguageLinksPropertyProvider : WikiPagePropertyProvider<LanguageLinksPropertyGroup>
{

    public LanguageLinksPropertyProvider(LanguageLinkProperties languageLinkProperties)
    {
        LanguageLinkProperties = languageLinkProperties;
    }

    public LanguageLinksPropertyProvider() : this(LanguageLinkProperties.None)
    {
    }

    /// <inheritdoc />
    public override string? PropertyName => "langlinks";

    /// <summary>
    /// Specify the additional language link properties to retrieve.
    /// </summary>
    public LanguageLinkProperties LanguageLinkProperties { get; set; }

    /// <summary>
    /// When <see cref="LanguageLinkProperties"/> has <see cref="Properties.LanguageLinkProperties.LanguageName"/> set,
    /// specifies the display language of the language names.
    /// </summary>
    public string? LanguageNameLanguage { get; set; }

    /// <summary>
    /// Only returns the interwiki link for this language code.
    /// </summary>
    public string? LanguageName { get; set; }

    public override IEnumerable<KeyValuePair<string, object?>> EnumParameters(MediaWikiVersion version)
    {
        // Limit is 500 for user, and 5000 for bots. We take 300 in a batch.
        var p = new OrderedKeyValuePairs<string, object?> { { "lllimit", 300 } };
        if (LanguageLinkProperties != LanguageLinkProperties.None)
        {
            if (version >= new MediaWikiVersion(1, 23))
            {
                var llprop = "";
                if ((LanguageLinkProperties & LanguageLinkProperties.Url) == LanguageLinkProperties.Url)
                    llprop = "url";
                if ((LanguageLinkProperties & LanguageLinkProperties.LanguageName) == LanguageLinkProperties.LanguageName)
                    llprop = llprop.Length == 0 ? "langname" : (llprop + "|langname");
                if ((LanguageLinkProperties & LanguageLinkProperties.Autonym) == LanguageLinkProperties.Autonym)
                    llprop = llprop.Length == 0 ? "autonym" : (llprop + "|autonym");
                p.Add("llprop", llprop);
            }
            else if (LanguageLinkProperties == LanguageLinkProperties.Url)
            {
                p.Add("llurl", true);
            }
            else
            {
                throw new NotSupportedException("MediaWiki 1.22- only supports LanguageLinkProperties.Url.");
            }
        }
        if (LanguageName != null)
            p.Add("lllang", LanguageName);
        if (LanguageNameLanguage != null)
            p.Add("llinlanguagecode", LanguageNameLanguage);
        return p;
    }

    public override LanguageLinksPropertyGroup? ParsePropertyGroup(JsonObject json)
    {
        if (json == null) throw new ArgumentNullException(nameof(json));
        return LanguageLinksPropertyGroup.Create(json);
    }

}

/// <summary>
/// The additional language link properties to retrieve for <see cref="LanguageLinksPropertyGroup"/>.
/// </summary>
[Flags]
public enum LanguageLinkProperties
{

    None = 0,

    /// <summary>Adds the full URL. (MW 1.23+, or MW 1.17+ in compatible mode)</summary>
    Url = 1,

    /// <summary>Adds the localized language name (best effort, use CLDR extension). Use llinlanguagecode to control the language. (MW 1.23+)</summary>
    LanguageName = 2,

    /// <summary>Adds the native language name. (MW 1.23+)</summary>
    Autonym = 4

}

/// <summary>
/// Represents the information about a language link.
/// </summary>
/// <seealso cref="LanguageLinksPropertyProvider"/>
/// <seealso cref="LanguageLinksPropertyGroup"/>
/// <seealso cref="ParsedContentInfo"/>
/// <remarks>
/// See <a href="https://github.com/wikimedia/mediawiki/blob/master/includes/api/ApiQueryLangLinks.php">ApiQueryLangLinks.php</a>.
/// </remarks>
[JsonContract]
public sealed class LanguageLinkInfo
{

    [JsonPropertyName("lang")]
    public required string Language { get; init; }

    /// <summary>URL of the language link target.</summary>
    /// <remarks>In rare cases, such as when the <see cref="Title"/> is malformed, this property can be <c>null</c>.</remarks>
    public string? Url { get; init; }

    /// <summary>
    /// Autonym of the language.
    /// </summary>
    public required string Autonym { get; init; }

    /// <summary>
    /// Title of the page in the specified language.
    /// </summary>
    [JsonPropertyName("*")]
    public required string Title { get; init; }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{Language}:{Title}";
    }

}

public class LanguageLinksPropertyGroup : WikiPagePropertyGroup
{

    private static readonly LanguageLinksPropertyGroup Empty = new LanguageLinksPropertyGroup(Array.Empty<LanguageLinkInfo>());

    internal static LanguageLinksPropertyGroup? Create(JsonObject jpage)
    {
        var jlangLinks = jpage["langlinks"];
        if (jlangLinks == null) return null;
        if (jpage.Count == 0) return Empty;
        var langLinks = jlangLinks.Deserialize<IReadOnlyCollection<LanguageLinkInfo>>(MediaWikiHelper.WikiJsonSerializerOptions);
        return new LanguageLinksPropertyGroup(langLinks!);
    }

    private LanguageLinksPropertyGroup(IReadOnlyCollection<LanguageLinkInfo> languageLinks)
    {
        Debug.Assert(languageLinks != null);
        LanguageLinks = languageLinks;
    }

    /// <summary>Retrieved language links.</summary>
    public IReadOnlyCollection<LanguageLinkInfo> LanguageLinks { get; }

}
