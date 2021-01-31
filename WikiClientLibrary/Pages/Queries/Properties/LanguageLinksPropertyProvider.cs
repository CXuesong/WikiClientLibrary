using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages.Parsing;

namespace WikiClientLibrary.Pages.Queries.Properties
{

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
        public override string PropertyName => "langlinks";

        /// <summary>
        /// Specify the additional language link properties to retrieve.
        /// </summary>
        public LanguageLinkProperties LanguageLinkProperties { get; set; }

        /// <summary>
        /// When <see cref="LanguageLinkProperties"/> has <see cref="Properties.LanguageLinkProperties.LanguageName"/> set,
        /// specifies the display language of the language names.
        /// </summary>
        public string LanguageNameLanguage { get; set; }

        /// <summary>
        /// Only returns the interwiki link for this language code.
        /// </summary>
        public string LanguageName { get; set; }

        public override IEnumerable<KeyValuePair<string, object>> EnumParameters(MediaWikiVersion version)
        {
            // Limit is 500 for user, and 5000 for bots. We take 300 in a batch.
            var p = new OrderedKeyValuePairs<string, object> { { "lllimit", 300 } };
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

        public override LanguageLinksPropertyGroup ParsePropertyGroup(JObject json)
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
    [JsonObject(MemberSerialization.OptIn)]
    public class LanguageLinkInfo
    {

        [JsonProperty("lang")]
        public string Language { get; private set; }

        [JsonProperty]
        public string Url { get; private set; }

        /// <summary>
        /// Autonym of the language.
        /// </summary>
        [JsonProperty]
        public string Autonym { get; private set; }

        /// <summary>
        /// Title of the page in the specified language.
        /// </summary>
        [JsonProperty("*")]
        public string Title { get; private set; }

        [Obsolete("Use Title property instead.")]
        public string PageTitle => Title;

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{Language}:{Title}";
        }

    }

    public class LanguageLinksPropertyGroup : WikiPagePropertyGroup
    {

        private static readonly LanguageLinksPropertyGroup Empty = new LanguageLinksPropertyGroup(Array.Empty<LanguageLinkInfo>());

        internal static LanguageLinksPropertyGroup Create(JToken jpage)
        {
            var jlangLinks = jpage["langlinks"];
            if (jlangLinks == null || !jlangLinks.HasValues)
                return Empty;
            var langLinks = jlangLinks.ToObject<IReadOnlyCollection<LanguageLinkInfo>>(Utility.WikiJsonSerializer);
            return new LanguageLinksPropertyGroup(langLinks);
        }

        private LanguageLinksPropertyGroup(IReadOnlyCollection<LanguageLinkInfo> languageLinks)
        {
            LanguageLinks = languageLinks;
        }

        /// <summary>Retrieved language links.</summary>
        public IReadOnlyCollection<LanguageLinkInfo> LanguageLinks { get; }

    }

}
