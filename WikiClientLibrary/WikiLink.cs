using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary
{

    /// <summary>
    /// An immutable representation of a MediaWiki link (local or interwiki).
    /// </summary>
    /// <remarks>
    /// A WikiLink expression is in the form of <c>interwiki:Namespace:Title#section|anchor</c>.
    /// </remarks>
    public class WikiLink
    {
        /// <summary>
        /// A regular expression used to match illegal titles.
        /// </summary>
        // From Pywikibot. page.py, Link class.
        public static readonly Regex IllegalTitlesPattern = new Regex(
        // Matching titles will be held as illegal.
            @"[\x00-\x1f\x23\x3c\x3e\x5b\x5d\x7b\x7c\x7d\x7f]"
        // URL percent encoding sequences interfere with the ability
        // to round-trip titles -- you can't link to them consistently.
            + "|%[0-9A-Fa-f]{2}"
        // XML/HTML character references produce similar issues.
            + @"|&[A-Za-z0-9\x80-\xff]+;"
            + "|&#[0-9]+"
            + "|&#x[0-9A-Fa-f]+;"
            );

        /// <inheritdoc cref="ParseAsync(WikiSite,IWikiFamily,string,int)"/>
        public static Task<WikiLink> ParseAsync(WikiSite site, IWikiFamily family, string text)
        {
            return ParseAsync(site, family, text, 0);
        }

        /// <inheritdoc cref="ParseAsync(IWikiFamily,string,int)"/>
        public static Task<WikiLink> ParseAsync(IWikiFamily family, string text)
        {
            return ParseAsync(family, text, 0);
        }

        /// <inheritdoc cref="ParseAsync(WikiSite,IWikiFamily,string,int)"/>
        /// <summary>
        /// Parses a new instance from the a Wikilink expression on the specified Wiki family.
        /// This overload resolves the target interwiki site with the interwiki prefixes provided
        /// <seealso cref="IWikiFamily"/> instance, and requires <paramref name="text"/> to have interwiki prefix.
        /// </summary>
        public static Task<WikiLink> ParseAsync(IWikiFamily family, string text, int defaultNamespaceId)
        {
            if (family == null) throw new ArgumentNullException(nameof(family));
            return ParseAsync(null, family, text, defaultNamespaceId);
        }

        /// <summary>
        /// Parses a new instance from the a Wikilink expression on the specified Wiki site and Wiki family.
        /// This overload also resolves the target interwiki site with the interwiki prefixes provided
        /// <seealso cref="IWikiFamily"/> instance.
        /// </summary>
        /// <param name="site">Site instance.</param>
        /// <param name="family">Wiki family. You need to provide this argument if you want to parse into interwiki links.</param>
        /// <param name="text">Wikilink expression, without square brackets.</param>
        /// <param name="defaultNamespaceId">Id of default namespace. See <see cref="BuiltInNamespaces"/> for a list of possible values.</param>
        /// <returns>The parsed <see cref="WikiLink"/> instance.</returns>
        /// <exception cref="ArgumentNullException">
        /// Both <paramref name="site"/> and <paramref name="family"/> is <c>null</c>.
        /// -- or --
        /// <paramref name="text"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="text"/> does not contain a valid page title.
        /// -- or --
        /// <paramref name="site"/> is <c>null</c>, but <paramref name="text"/> does not contain any interwiki prefix.
        /// </exception>
        public static Task<WikiLink> ParseAsync(WikiSite site, IWikiFamily family, string text, int defaultNamespaceId)
        {
            return ParseInternalAsync(site, family, text, defaultNamespaceId, true);
        }

        /// <inheritdoc cref="Parse(WikiSite,string,int)"/>
        public static WikiLink Parse(WikiSite site, string text)
        {
            return Parse(site, text, 0);
        }

        /// <summary>
        /// Parses a new instance using specified Wikilink expression.
        /// </summary>
        /// <param name="site">Site instance.</param>
        /// <param name="text">Wikilink expression, without square brackets.</param>
        /// <param name="defaultNamespaceId">Id of default namespace. See <see cref="BuiltInNamespaces"/> for a list of possible values.</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="site"/> or <paramref name="text"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="text"/> does not contain a valid page title.</exception>
        public static WikiLink Parse(WikiSite site, string text, int defaultNamespaceId)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            return ParseInternalAsync(site, null, text, defaultNamespaceId, true).GetAwaiter().GetResult();
        }

        /// <inheritdoc cref="TryParse(WikiSite,string,int)"/>
        public static WikiLink TryParse(WikiSite site, string text)
        {
            return TryParse(site, text, 0);
        }

        /// <inheritdoc cref="TryParseAsync(WikiSite,IWikiFamily,string,int)"/>
        public static Task<WikiLink> TryParseAsync(WikiSite site, IWikiFamily family, string text)
        {
            return TryParseAsync(site, family, text, 0);
        }

        /// <inheritdoc cref="TryParseAsync(IWikiFamily,string,int)"/>
        public static Task<WikiLink> TryParseAsync(IWikiFamily family, string text)
        {
            return TryParseAsync(family, text, 0);
        }

        /// <inheritdoc cref="TryParseAsync(WikiSite,IWikiFamily,string,int)"/>
        /// <summary>
        /// Tries to parse a new instance from the a Wikilink expression on the specified Wiki family.
        /// This overload resolves the target interwiki site with the interwiki prefixes provided
        /// <seealso cref="IWikiFamily"/> instance, and requires <paramref name="text"/> to have interwiki prefix.
        /// </summary>
        public static Task<WikiLink> TryParseAsync(IWikiFamily family, string text, int defaultNamespaceId)
        {
            if (family == null) throw new ArgumentNullException(nameof(family));
            return TryParseAsync(null, family, text, defaultNamespaceId);
        }

        /// <summary>
        /// Parses a new instance from the a Wikilink expression on the specified Wiki site and Wiki family.
        /// This overload also resolves the target interwiki site with the interwiki prefixes provided
        /// <seealso cref="IWikiFamily"/> instance.
        /// </summary>
        /// <param name="site">Site instance.</param>
        /// <param name="family">Wiki family. You need to provide this argument if you want to parse into interwiki links.</param>
        /// <param name="text">Wikilink expression, without square brackets.</param>
        /// <param name="defaultNamespaceId">Id of default namespace. See <see cref="BuiltInNamespaces"/> for a list of possible values.</param>
        /// <returns>A <see cref="WikiLink"/> instance, or <c>null</c> if the parsing failed.</returns>
        /// <exception cref="ArgumentNullException">
        /// Both <paramref name="site"/> and <paramref name="family"/> is <c>null</c>.
        /// -- or --
        /// <paramref name="text"/> is <c>null</c>.
        /// </exception>
        public static Task<WikiLink> TryParseAsync(WikiSite site, IWikiFamily family, string text, int defaultNamespaceId)
        {
            return ParseInternalAsync(site, family, text, defaultNamespaceId, false);
        }

        /// <summary>
        /// Tries to parse a new instance using specified Wikilink expression.
        /// </summary>
        /// <param name="site">Site instance.</param>
        /// <param name="text">Wikilink expression, without square brackets.</param>
        /// <param name="defaultNamespaceId">Id of default namespace. See <see cref="BuiltInNamespaces"/> for a list of possible values.</param>
        /// <returns>A <see cref="WikiLink"/> instance, or <c>null</c> if the parsing failed.</returns>
        /// <exception cref="ArgumentNullException">Either <paramref name="site"/> or <paramref name="text"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="text"/> does not contain a valid page title.</exception>

        public static WikiLink TryParse(WikiSite site, string text, int defaultNamespaceId)
        {
            return ParseInternalAsync(site, null, text, defaultNamespaceId, false).GetAwaiter().GetResult();
        }

        private static async Task<WikiLink> ParseInternalAsync(WikiSite site, IWikiFamily family, string text, int defaultNamespaceId, bool exceptionOnFailure)
        {
            if (site == null && family == null)
                throw new ArgumentNullException(nameof(site) + "/" + nameof(family));
            if (text == null) throw new ArgumentNullException(nameof(text));
            //preprocess text (these changes aren't site-dependent)
            //First remove anchor, which is stored unchanged, if there is one
            var pipePos = text.IndexOf('|');
            var title = pipePos >= 0 ? text.Substring(0, pipePos) : text;
            var anchor = pipePos >= 0 ? text.Substring(pipePos + 1) : null;
            //This code was adapted from Title.php : secureAndSplit()
            if (title.IndexOf('\ufffd') >= 0)
            {
                if (exceptionOnFailure)
                    throw new ArgumentException(string.Format(Prompts.ExceptionTitleIllegalCharacter1, "\uFFFD 'REPLACEMENT CHARACTER'"), nameof(text));
                return null;
            }
            var hashPos = title.IndexOf('#');
            var section = hashPos >= 0 ? title.Substring(hashPos + 1) : null;
            title = hashPos >= 0 ? title.Substring(0, hashPos) : title;
            var match = IllegalTitlesPattern.Match(title);
            if (match.Success)
            {
                if (exceptionOnFailure)
                    throw new ArgumentException(string.Format(Prompts.ExceptionTitleIllegalCharacterSequence1, match.Value));
                return null;
            }
            //Parse title parts.
            var parsedTitle = await TitlePartitionAsync(site, family, title, defaultNamespaceId);
            if (parsedTitle == null)
            {
                if (exceptionOnFailure)
                    throw new ArgumentException(string.Format(Prompts.ExceptionTitleIsEmpty1, title));
                return null;
            }
            var targetSite = parsedTitle.Item1;
            var interwikiPrefix = parsedTitle.Item2;
            var nsPrefix = parsedTitle.Item3;
            var localTitle = parsedTitle.Item4;
            if (targetSite == null)
            {
                if (interwikiPrefix == null)
                {
                    // No interwiki prefix
                    // This means we are parsing WikiLink without originating WikiSite.
                    Debug.Assert(site == null);
                    if (exceptionOnFailure)
                        throw new ArgumentException(Prompts.ExceptionWikiLinkRequireInterwikiPrefix, nameof(text));
                    return null;
                }
                // If we do not have wiki family information, and there IS an interwiki prefix,
                // subsequent namespace will not be parsed and will be left as a part of Name
                Debug.Assert(nsPrefix == null);
                Debug.Assert(localTitle != null);
            }
            var link = new WikiLink(site, text)
            {
                Anchor = anchor,
                Section = section,
                InterwikiPrefix = interwikiPrefix,
                NamespaceName = nsPrefix,
                Title = localTitle,
                FullTitle = localTitle,
                TargetSite = targetSite
            };
            link.Namespace = nsPrefix != null && link.TargetSite != null
                ? link.TargetSite.Namespaces[nsPrefix]
                : null;
            //Format expression.
            var sb = new StringBuilder();
            if (link.InterwikiPrefix != null)
            {
                sb.Append(link.InterwikiPrefix);
                sb.Append(':');
            }
            var fullTitleStartsAt = sb.Length;
            if (!string.IsNullOrEmpty(link.NamespaceName))
            {
                sb.Append(link.NamespaceName);
                sb.Append(':');
                link.FullTitle = link.NamespaceName + ":" + link.Title;
            }
            sb.Append(link.Title);
            if (link.Section != null)
            {
                sb.Append('#');
                sb.Append(link.Section);
            }
            link.Target = sb.ToString();
            link.FullTitleAndSection = fullTitleStartsAt == 0 ? link.Target : sb.ToString(fullTitleStartsAt, sb.Length - fullTitleStartsAt);
            if (link.Anchor != null)
            {
                sb.Append('|');
                sb.Append(link.Anchor);
            }
            link._FormattedText = sb.ToString();
            return link;
        }

        /// <summary>
        /// The original wiki site provided to resolve this wikilink.
        /// </summary>
        /// <remarks>
        /// The value can be <c>null</c> if this <seealso cref="WikiLink"/> is parsed without
        /// originating <seealso cref="WikiSite"/> information,
        /// e.g. parsed with only <seealso cref="IWikiFamily"/> provided.
        /// </remarks>
        /// <seealso cref="TargetSite"/>
        public WikiSite Site { get; }

        /// <summary>
        /// Gets the wiki site containing the specified page title, if such information is available.
        /// </summary>
        /// <value>
        /// If the parsed wikilink expression does not contain interwiki prefix,
        /// this property is the same as <see cref="Site"/>.
        /// If this wikilink is parsed with no <see cref="IWikiFamily"/> provided,
        /// while it contains interwiki prefix, this property will be <c>null</c>.
        /// </value>
        /// <seealso cref="Site"/>
        public WikiSite TargetSite { get; private set; }

        private WikiLink(WikiSite site, string originalText)
        {
            this.Site = site;
            this.OriginalText = originalText;
        }

        private static async Task<Tuple<WikiSite, string, string, string>> TitlePartitionAsync(WikiSite site, IWikiFamily family, string rawTitle, int defaultNamespace)
        {
            // Tuple<interwiki, namespace, title, targetSite>
            Debug.Assert(site != null || family != null);
            Debug.Assert(rawTitle != null);
            var title = rawTitle;
            if (title.Length == 0)
                return null;
            var state = 0;
            /*
             state  accepts
             0      LeadingBlank
             1      Namespace / Interwiki
             2      Page title
             */
            string interwiki = null, nsname = null, pagetitle = null;
            while (title != null)
            {
                var parts = title.Split(new[] { ':' }, 2);
                var part = parts[0].Trim(' ', '_');
                switch (state)
                {
                    case 0:
                        if (part.Length > 0) goto case 1;
                        // Initial colon indicates main namespace rather than default.
                        nsname = "";
                        state = 1;
                        break;
                    case 1:
                        // Make sure there's a colon ahead; otherwise we just treat it as a normal title.
                        if (parts.Length == 1) goto case 2;
                        string normalizedInterwikiPrefix;
                        if (site != null && site.Namespaces.TryGetValue(part, out var ns))
                        {
                            // This is a namespace name.
                            nsname = ns.CustomName;
                            state = 2;
                        }
                        else if (family != null && (normalizedInterwikiPrefix = family.TryNormalize(part)) != null)
                        {
                            // This is a known prefix in the specified WikiFamily.
                            var nextSite = await family.GetSiteAsync(part);
                            if (nextSite == null)
                            {
                                Debug.Assert(false, $"{family} returned null for prefix: {normalizedInterwikiPrefix}. " +
                                                    "IWikiFamily.TryNormalize should return null for in-existent interwiki prefixes.");
                            }
                            else
                            {
                                // We have bumped into another wiki, hooray!
                                interwiki = normalizedInterwikiPrefix;
                                site = nextSite;
                                // state will still be 1, to parse namespace or other interwikis (rare)
                            }
                        }
                        else if (site != null && site.InterwikiMap.Contains(part))
                        {
                            // Otherwise, check whether this is an interwiki prefix.
                            interwiki = part.ToLowerInvariant();
                            // For interwiki, we do not parse namespace name.
                            // Instead, we treat it as a part of page title.
                            nsname = null;
                            state = 2;
                        }
                        else
                        {
                            // So this is only the beginning of a normal title.
                            goto case 2;
                        }
                        break;
                    case 2:
                        pagetitle = Utility.NormalizeTitlePart(title, site?.SiteInfo.IsTitleCaseSensitive ?? true);
                        goto END_OF_PARSING;
                }
                title = parts[1];
            }
        END_OF_PARSING:
            Debug.Assert(pagetitle != null, "pagetitle != null");
            if (pagetitle.Length == 0)
                return null;
            // nsname == null but interwiki != null means that the expression has interwiki prefix, while family == null
            if (nsname == null && interwiki == null)
                // If site is (still) null, we will have error reported in the caller.
                nsname = site?.Namespaces[defaultNamespace].CustomName;
            return Tuple.Create(site, interwiki, nsname, pagetitle);
        }

        /// <summary>
        /// Gets interwiki prefix of the wikilink.
        /// </summary>
        /// <value>Interwiki prefix of the wikilink. If this link is not an interwiki link, the value is <c>null</c>.</value>
        public string InterwikiPrefix { get; private set; }

        /// <summary>
        /// Gets the namespace name.
        /// </summary>
        /// <value>
        /// If the title is in main namespace, the value is empty string.
        /// If this is not applicable (e.g. parsed a interwiki link without family information given),
        /// the value is <c>null</c>.
        /// </value>
        public string NamespaceName { get; private set; }

        /// <summary>
        /// Gets the namespace information.
        /// </summary>
        public NamespaceInfo Namespace { get; private set; }

        /// <summary>
        /// Gets the target page title, excluding all the prefix and section. (<c>Title</c>)
        /// </summary>
        public string Title { get; private set; }

        /// <summary>
        /// Gets the full title of the target page. (<c>Namespace:Title</c>)
        /// </summary>
        /// <value>
        /// Title of the page with namespace prefix, if prefix exists.
        /// </value>
        public string FullTitle { get; private set; }

        /// <summary>
        /// Gets the section title of a section on the page. (<c>Section</c>)
        /// </summary>
        /// <value>
        /// The section title of a section on the page, without leading #.
        /// <c>null</c> if the link does not have section information.
        /// </value>
        public string Section { get; private set; }

        /// <summary>
        /// Gets the full wikilink target expression. (<c>interwiki:Namespace:Title#Section</c>)
        /// </summary>
        /// <value>
        /// For wikilink expression in the form <c>[[target|anchor]]</c>,
        /// gets the full page title (i.e. <c>target</c> part) for the link,
        /// including interwiki prefix and section name.
        /// </value>
        /// <remarks>For wikilink without interwiki prefix, this property has the same value as <seealso cref="FullTitleAndSection"/>.</remarks>
        public string Target { get; private set; }

        /// <summary>
        /// Gets the full page title and section part of the wikilink. (<c>Namespace:Title#Section</c>)
        /// </summary>
        /// <value>Full page title and section part of the wikilink, without interwiki prefix.</value>
        /// <remarks>For wikilink without interwiki prefix, this property has the same value as <seealso cref="Target"/>.</remarks>
        public string FullTitleAndSection { get; private set; }

        /// <summary>
        /// For wikilink expression in the form <c>[[target|anchor]]</c>, excluding the square brackets,
        /// gets the actual displayed text (<c>anchor</c>) for the link.
        /// </summary>
        /// <remarks>For the actual text this wikilink should show, use <see cref="DisplayText"/>.</remarks>
        public string Anchor { get; private set; }

        private string _DisplayText;

        /// <summary>
        /// Gets the actual link text that should be shown.
        /// </summary>
        /// <value>
        /// <see cref="Anchor"/>, if the value is not <c>null</c>; otherwise <see cref="Target"/>.
        /// if <see cref="Anchor"/> is <see cref="string.Empty"/>,
        /// this property returns the text after the first colon in <see cref="Target"/>.
        /// See <a href="https://en.wikipedia.org/wiki/Help:Pipe_trick">w:H:Pipe trick</a> for more information.
        /// </value>
        public string DisplayText
        {
            get
            {
                var localValue = _DisplayText;
                if (localValue == null)
                {
                    if (Anchor == null)
                    {
                        localValue = Target;
                    }
                    else if (Anchor.Length == 0)
                    {
                        localValue = Target;
                        var colonPos = localValue.IndexOf(':');
                        if (colonPos >= 0) localValue = localValue.Substring(colonPos + 1);
                    }
                    else
                    {
                        localValue = Anchor;
                    }
                    _DisplayText = localValue;
                }
                return localValue;
            }
        }

        /// <summary>
        /// Gets the original wikitext expression that was passed to the Parse or ParseAsync methods.
        /// </summary>
        public string OriginalText { get; }

        private string _TargetUrl;

        /// <summary>
        /// Gets the full URL of the wikilink target.
        /// </summary>
        /// <remarks>This property uses <see cref="SiteInfo.MakeArticleUrl(string)"/> to build the article URL.</remarks>
        public string TargetUrl
        {
            get
            {
                var localValue = _TargetUrl;
                if (localValue == null)
                {
                    localValue = TargetSite == null
                        ? Site.SiteInfo.MakeArticleUrl(Target)
                        : TargetSite.SiteInfo.MakeArticleUrl(FullTitleAndSection);
                    _TargetUrl = localValue;
                }
                return localValue;
            }
        }

        private string _FormattedText;

        /// <summary>
        /// Gets the formatted expression of the wikilink.
        /// </summary>
        /// <returns>The wikilink expression, excluding the surrounding square brackets [[ ]].</returns>
        public override string ToString()
        {
            return _FormattedText;
        }

        /// <summary>
        /// Uses this class to normalize a specific wikilink expression.
        /// </summary>
        /// <param name="site">Site instance.</param>
        /// <param name="text">Wikilink expression, without square brackets.</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="site"/> or <paramref name="text"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="text"/> does not contain a valid page title.</exception>
        /// <returns>Normalized wikilink expression.</returns>
        public static string NormalizeWikiLink(WikiSite site, string text)
        {
            return NormalizeWikiLink(site, text, BuiltInNamespaces.Main);
        }

        /// <summary>
        /// Uses this class to normalize a specific wikilink expression.
        /// </summary>
        /// <param name="site">Site instance.</param>
        /// <param name="text">Wikilink expression, without square brackets.</param>
        /// <param name="defaultNamespaceId">Id of default namespace.</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="site"/> or <paramref name="text"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="text"/> does not contain a valid page title.</exception>
        /// <returns>Normalized wikilink expression.</returns>
        public static string NormalizeWikiLink(WikiSite site, string text, int defaultNamespaceId)
        {
            var link = Parse(site, text, defaultNamespaceId);
            return link._FormattedText;
        }
    }

}
