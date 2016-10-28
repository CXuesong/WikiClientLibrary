﻿using System;
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

        /// <summary>
        /// Parses a new instance using specified Wikilink expression.
        /// </summary>
        /// <param name="site">Site instance.</param>
        /// <param name="text">Wikilink expression, without square brackets.</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="site"/> or <paramref name="text"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="text"/> does not contain a valid page title.</exception>

        public static WikiLink Parse(Site site, string text)
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

        public static WikiLink Parse(Site site, string text, int defaultNamespaceId)
        {
            return ParseInternal(site, text, defaultNamespaceId, true);
        }

        /// <summary>
        /// Tries to parse a new instance using specified Wikilink expression.
        /// </summary>
        /// <param name="site">Site instance.</param>
        /// <param name="text">Wikilink expression, without square brackets.</param>
        /// <returns>A <see cref="WikiLink"/> instance, or <c>null</c> if the parsing failed.</returns>
        /// <exception cref="ArgumentNullException">Either <paramref name="site"/> or <paramref name="text"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="text"/> does not contain a valid page title.</exception>

        public static WikiLink TryParse(Site site, string text)
        {
            return TryParse(site, text, 0);
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

        public static WikiLink TryParse(Site site, string text, int defaultNamespaceId)
        {
            return ParseInternal(site, text, defaultNamespaceId, false);
        }

        private static WikiLink ParseInternal(Site site, string text, int defaultNamespaceId, bool exceptionOnFailed)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (text == null) throw new ArgumentNullException(nameof(text));
            var link = new WikiLink(site, text);
            //preprocess text (these changes aren't site-dependent)
            //First remove anchor, which is stored unchanged, if there is one
            var parts = text.Split(new[] { '|' }, 2);
            var title = parts[0];
            link.Anchor = parts.Length > 1 ? parts[1] : null;
            //This code was adapted from Title.php : secureAndSplit()
            if (title.IndexOf('\ufffd') >= 0)
            {
                if (exceptionOnFailed)
                    throw new ArgumentException("Title contains illegal char (\\uFFFD 'REPLACEMENT CHARACTER')",
                        nameof(text));
                return null;
            }
            parts = title.Split(new[] { '#' }, 2);
            title = parts[0];
            link.Section = parts.Length > 1 ? parts[1] : null;
            var match = IllegalTitlesPattern.Match(title);
            if (match.Success)
            {
                if (exceptionOnFailed)
                    throw new ArgumentException($"Title contains illegal char sequence: {match.Value} .");
                return null;
            }
            //Parse title parts.
            var parsedTitle = ParseTitle(site, title, defaultNamespaceId);
            link.InterwikiPrefix = parsedTitle.Item1;
            link.NamespaceName = parsedTitle.Item2;
            link.Title = parsedTitle.Item3;
            link.Interwiki = parsedTitle.Item1 == null ? null : site.InterwikiMap[parsedTitle.Item1];
            link.Namespace = parsedTitle.Item2 == null ? null : site.Namespaces[parsedTitle.Item2];
            //Format expression.
            var sb = new StringBuilder();
            if (link.InterwikiPrefix != null)
            {
                sb.Append(link.InterwikiPrefix);
                sb.Append(':');
            }
            if (!string.IsNullOrEmpty(link.NamespaceName))
            {
                sb.Append(link.NamespaceName);
                sb.Append(':');
            }
            sb.Append(link.Title);
            if (link.Section != null)
            {
                sb.Append('#');
                sb.Append(link.Section);
            }
            if (link.Anchor != null)
            {
                sb.Append('|');
                sb.Append(link.Anchor);
            }
            link._FormattedText = sb.ToString();
            return link;
        }

        public Site Site { get; }

        private WikiLink(Site site, string originalText)
        {
            this.Site = site;
            this.OriginalText = originalText;
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

        public string InterwikiPrefix { get; private set; }

        public InterwikiEntry Interwiki { get; private set; }

        public string NamespaceName { get; private set; }

        public NamespaceInfo Namespace { get; private set; }

        public string Title { get; private set; }

        public string Section { get; private set; }

        public string Anchor { get; private set; }

        public string OriginalText { get; }

        private string _FormattedText;

        /// <summary>
        /// Gets the formatted expression of the wikilink.
        /// </summary>
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
        public static string NormalizeWikiLink(Site site, string text)
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
        public static string NormalizeWikiLink(Site site, string text, int defaultNamespaceId)
        {
            var link = Parse(site, text, defaultNamespaceId);
            return link._FormattedText;
        }
    }
}