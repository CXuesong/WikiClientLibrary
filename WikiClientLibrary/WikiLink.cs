using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
    
        public Site Site { get; }

        /// <summary>
        /// Initializes a new instance using specified Wikilink expression.
        /// </summary>
        /// <param name="site">Site instance.</param>
        /// <param name="text">Wikilink expression, without square brackets.</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="site"/> or <paramref name="text"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="text"/> does not contain a valid page title.</exception>
        public WikiLink(Site site, string text) : this(site, text, BuiltInNamespaces.Main)
        {
        }

        /// <summary>
        /// Initializes a new instance using specified Wikilink expression.
        /// </summary>
        /// <param name="site">Site instance.</param>
        /// <param name="text">Wikilink expression, without square brackets.</param>
        /// <param name="defaultNamespaceId">Id of default namespace.</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="site"/> or <paramref name="text"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="text"/> does not contain a valid page title.</exception>
        public WikiLink(Site site, string text, int defaultNamespaceId)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (text == null) throw new ArgumentNullException(nameof(text));
            Site = site;
            OriginalText = text;
            //preprocess text (these changes aren't site-dependent)
            //First remove anchor, which is stored unchanged, if there is one
            var parts = text.Split(new[] {'|'}, 2);
            var title = parts[0];
            Anchor = parts.Length > 1 ? parts[1] : null;
            //This code was adapted from Title.php : secureAndSplit()
            if (title.IndexOf('\ufffd') >= 0)
                throw new ArgumentException("Title contains illegal char (\\uFFFD 'REPLACEMENT CHARACTER')",
                    nameof(text));
            parts = title.Split(new[] {'#'}, 2);
            title = parts[0];
            Section = parts.Length > 1 ? parts[1] : null;
            var match = IllegalTitlesPattern.Match(title);
            if (match.Success)
                throw new ArgumentException($"Title contains illegal char sequence: {match.Value} .");
            //Parse title parts.
            var parsedTitle = ParseTitle(site, title, defaultNamespaceId);
            InterwikiPrefix = parsedTitle.Item1;
            NamespaceName = parsedTitle.Item2;
            Title = parsedTitle.Item3;
            Interwiki = parsedTitle.Item1 == null ? null : site.InterwikiMap[parsedTitle.Item1];
            Namespace = parsedTitle.Item2 == null ? null : site.Namespaces[parsedTitle.Item2];
            //Format expression.
            var sb = new StringBuilder();
            if (InterwikiPrefix != null)
            {
                sb.Append(InterwikiPrefix);
                sb.Append(':');
            }
            if (!string.IsNullOrEmpty(NamespaceName))
            {
                sb.Append(NamespaceName);
                sb.Append(':');
            }
            sb.Append(Title);
            if (Section != null)
            {
                sb.Append('#');
                sb.Append(Section);
            }
            if (Anchor != null)
            {
                sb.Append('|');
                sb.Append(Anchor);
            }
            _FormattedText = sb.ToString();
        }

        private static Tuple<string, string, string> ParseTitle(Site site, string rawTitle, int defaultNamespace)
        {
            // Tuple<interwiki, namespace, title>
            Debug.Assert(site != null);
            Debug.Assert(rawTitle != null);
            var title = rawTitle;
            if (title.Length == 0) goto EMPTY_TITLE;
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
                var parts = title.Split(new[] {':'}, 2);
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
                        NamespaceInfo ns;
                        if (site.Namespaces.TryGetValue(part, out ns))
                        {
                            // This is a namespace name.
                            nsname = ns.CustomName;
                            state = 2;
                        } else if (site.InterwikiMap.Contains(part))
                        {
                            // Otherwise, check whether this is an interwiki prefix.
                            interwiki = part.ToLowerInvariant();
                            // For interwiki, we do not parse namespace name.
                            // Instead, we treat it as a part of page title.
                            nsname = "";
                            state = 2;
                        }
                        else
                        {
                            // So this is only the beginning of a normal title.
                            goto case 2;
                        }
                        break;
                    case 2:
                        pagetitle = Utility.NormalizeTitlePart(title, site.SiteInfo.IsTitleCaseSensitive);
                        goto END_OF_PARSING;
                }
                title = parts[1];
            }
            END_OF_PARSING:
            Debug.Assert(pagetitle != null, "pagetitle != null");
            if (pagetitle.Length == 0) goto EMPTY_TITLE;
            if (nsname == null) nsname = site.Namespaces[defaultNamespace].CustomName;
            return Tuple.Create(interwiki, nsname, pagetitle);
            EMPTY_TITLE:
            throw new ArgumentException($"The title \"{rawTitle}\" does not contain page title.");
        }

        public string InterwikiPrefix { get; }

        public InterwikiEntry Interwiki { get; }

        public string NamespaceName { get; }

        public NamespaceInfo Namespace { get; }

        public string Title { get; }

        public string Section { get; }

        public string Anchor { get; }

        public string OriginalText { get; }

        private readonly string _FormattedText;

        /// <summary>
        /// Gets the formatted expression of the wikilink.
        /// </summary>
        public override string ToString()
        {
            return _FormattedText;
        }
    }
}
