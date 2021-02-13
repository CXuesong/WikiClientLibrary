using System;
using System.Collections.Generic;

namespace WikiClientLibrary
{
    /// <summary>
    /// Contains MediaWiki built-in namespace IDs for most MediaWiki sites. (MediaWiki 1.14+)
    /// </summary>
    public static class BuiltInNamespaces
    {
        public const int Media = -2;
        public const int Special = -1;
        public const int Main = 0;
        public const int Talk = 1;
        public const int User = 2;
        public const int UserTalk = 3;
        public const int Project = 4;
        public const int ProjectTalk = 5;
        public const int File = 6;
        public const int FileTalk = 7;
        public const int MediaWiki = 8;
        public const int MediaWikiTalk = 9;
        public const int Template = 10;
        public const int TemplateTalk = 11;
        public const int Help = 12;
        public const int HelpTalk = 13;
        public const int Category = 14;
        public const int CategoryTalk = 15;

        private static readonly IDictionary<int, string> _CanonicalNameDict = new Dictionary<int, string>
        {
            {-2, "Media"},
            {-1, "Special"},
            {0, ""},
            {1, "Talk"},
            {2, "User"},
            {3, "User talk"},
            {4, "Project"},
            {5, "Project talk"},
            {6, "File"},
            {7, "File talk"},
            {8, "MediaWiki"},
            {9, "MediaWiki talk"},
            {10, "Template"},
            {11, "Template talk"},
            {12, "Help"},
            {13, "Help talk"},
            {14, "Category"},
            {15, "Category talk"},
        };

        /// <summary>
        /// Gets the canonical name for a specific built-in namespace.
        /// </summary>
        /// <param name="namespaceId">A built-in namespace id.</param>
        /// <returns>
        /// canonical name for the specified built-in namespace.
        /// OR <c>null</c> if no such namespace is found.
        /// </returns>
        public static string? GetCanonicalName(int namespaceId)
        {
            if (_CanonicalNameDict.TryGetValue(namespaceId, out var name)) return name;
            return null;
        }
    }

    /// <summary>
    /// Contains commonly-used content model names for MediaWiki pages. (MediaWiki 1.22+)
    /// </summary>
    public static class ContentModels
    {
        /// <summary>
        /// Normal wikitext.
        /// </summary>
        public const string Wikitext = "wikitext";
        /// <summary>
        /// Javascript, as used in [[MediaWiki:Common.js]].
        /// </summary>
        public const string JavaScript = "javascript";
        /// <summary>
        /// CSS, as used in [[MediaWiki:Common.js]].
        /// </summary>
        public const string Css = "css";
        /// <summary>
        /// Scribunto LUA module.
        /// </summary>
        public const string Scribunto = "Scribunto";
        /// <summary>
        /// Flow board page.
        /// </summary>
        /// <remarks>See https://www.mediawiki.org/wiki/Extension:Flow/API .</remarks>
        public const string FlowBoard = "flow-board";
        /// <summary>
        /// JSON that describes an entity in Wikibase.
        /// </summary>
        public const string WikibaseItem = "wikibase-item";
    }

}