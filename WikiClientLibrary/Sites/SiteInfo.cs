using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages;

namespace WikiClientLibrary.Sites
{
    /// <summary>
    /// Provides read-only access to general site information.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class SiteInfo
    {
        private string _Generator;

        /// <summary>
        /// The title of the main page, as found in MediaWiki:Mainpage. (MediaWiki 1.8+)
        /// </summary>
        [JsonProperty] // This should be kept or private setter will be ignored by Newtonsoft.JSON.
        public string MainPage { get; private set; }

        #region URL & Path

        /// <summary>
        /// The absolute path to the main page. (MediaWiki 1.8+)
        /// </summary>
        [JsonProperty("base")]
        public string BaseUrl { get; private set; }

        /// <summary>
        /// The absolute or protocol-relative base URL for the server.
        /// (MediaWiki 1.16+, <a href="https://www.mediawiki.org/wiki/Manual:$wgServer">mw:Manual:$wgServer</a>)
        /// </summary>
        [JsonProperty("server")]
        public string ServerUrl { get; private set; }

        /// <summary>
        /// The relative or absolute path to any article. $1 should be replaced by the article name.
        /// (MediaWiki 1.16+, <a href="https://www.mediawiki.org/wiki/Manual:$wgArticlePath">mw:Manual:$wgArticlePath</a>)
        /// </summary>
        [JsonProperty]
        public string ArticlePath { get; private set; }

        /// <summary>
        /// The path of <c>index.php</c> relative to the document root.
        /// (MediaWiki 1.1.0+, <a href="https://www.mediawiki.org/wiki/Manual:$wgScript">mw:Manual:$wgScript</a>)
        /// </summary>
        [JsonProperty("script")]
        public string ScriptFilePath { get; private set; }

        /// <summary>
        /// The base URL path relative to the document root. 
        /// (MediaWiki 1.1.0+, <a href="https://www.mediawiki.org/wiki/Manual:$wgScriptPath">mw:Manual:$wgScriptPath</a>)
        /// </summary>
        [JsonProperty("scriptpath")]
        public string ScriptDirectoryPath { get; private set; }

        [JsonProperty("favicon")]
        public string FavIconUrl { get; private set; }

        private Tuple<string, string> articleUrlTemplateCache;

        /// <summary>
        /// Makes the full URL to the page of specified title.
        /// </summary>
        /// <param name="title">The title of the article.</param>
        /// <exception cref="ArgumentNullException"><paramref name="title"/> is <c>null</c>.</exception>
        /// <remarks>
        /// This overload uses <c>https:</c> as default protocol for protocol-relative URL.
        /// For wiki sites with specified-protocol <see cref="ServerUrl"/>, such as Wikia (which uses http),
        /// this overload respects the server-chosen protocol.
        /// </remarks>
        public string MakeArticleUrl(string title)
        {
            return MakeArticleUrl(title, "https");
        }

        /// <summary>
        /// Makes the full URL to the page of specified title.
        /// </summary>
        /// <param name="title">The title of the article.</param>
        /// <param name="defaultProtocol">
        /// For wiki sites whose <see cref="ServerUrl"/> is protocol-relative URL (e.g. <c>//en.wikipedia.org/</c>),
        /// specifies the default protocol to use. (e.g. <c>https</c>)</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="title"/> or <paramref name="defaultProtocol"/> is <c>null</c>.</exception>
        /// <returns>The full URL of the article.</returns>
        /// <remarks>
        /// For wiki sites with specified-protocol <see cref="ServerUrl"/>, such as Wikia (which uses http),
        /// this overload respects the server-chosen protocol.
        /// </remarks>
        public string MakeArticleUrl(string title, string defaultProtocol)
        {
            if (title == null) throw new ArgumentNullException(nameof(title));
            if (defaultProtocol == null) throw new ArgumentNullException(nameof(defaultProtocol));
            var cache = articleUrlTemplateCache;
            if (cache == null || cache.Item1 != defaultProtocol)
            {
                var urlTemplate = MediaWikiHelper.MakeAbsoluteUrl(ServerUrl, ArticlePath, defaultProtocol);
                cache = new Tuple<string, string>(defaultProtocol, urlTemplate);
                Volatile.Write(ref articleUrlTemplateCache, cache);
            }
            return cache.Item2.Replace("$1", Uri.EscapeUriString(title));
        }

        #endregion

        #region General

        [JsonProperty]
        public string SiteName { get; private set; }

        [JsonProperty("logo")]
        public string LogoUrl { get; private set; }

        /// <summary>
        /// API version information as found in $wgVersion. 1.8+
        /// </summary>
        /// <remarks>Example value: MediaWiki 1.28.0-wmf.15</remarks>
        [JsonProperty("generator")]
        public string Generator
        {
            get { return _Generator; }
            private set
            {
                _Generator = value;
                if (value != null)
                {
                    var part = value.Split(' ', '-');
                    Version = Version.Parse(part[1]);
                }
            }
        }

        /// <summary>
        /// Gets main part of API version. E.g. 1.28.0 for MediaWiki 1.28.0-wmf.15 .
        /// </summary>
        public Version Version { get; private set; }

        /// <summary>
        /// A list of magic words and their aliases 1.14+
        /// </summary>
        public string[] MagicWords { get; private set; }

        [JsonProperty("magicwords")]
        private JObject MagicWordsProxy
        {
            set { MagicWords = value.Properties().Select(p => p.Name).ToArray(); }
        }

        #endregion

        #region Limitations

        [JsonProperty]
        public long MaxUploadSize { get; private set; }

        [JsonProperty]
        public int MinUploadChunkSize { get; private set; }

        #endregion

        [JsonProperty]
        private string Case
        {
            set
            {
                switch (value)
                {
                    case "case-sensitive":
                        IsTitleCaseSensitive = true;
                        break;
                    case "first-letter":
                        IsTitleCaseSensitive = false;
                        break;
                    default:
                        throw new ArgumentException("Invalid case value.");
                }
            }
        }

        /// <summary>
        /// Whether the first letter in a title is case-sensitive. (MediaWiki 1.8+)
        /// </summary>
        public bool IsTitleCaseSensitive { get; private set; }

        /// <summary>
        /// The current time on the server. 1.16+
        /// </summary>
        [JsonProperty]
        public string Time { get; private set; }

        /// <summary>
        /// The name of the wiki's time zone. See $wgLocaltimezone. 1.13+
        /// </summary>
        /// <remarks>This will be used for date display and not for what's stored in the database.</remarks>
        [JsonProperty("timezone")]
        public string TimeZoneName { get; private set; }

        /// <summary>
        /// The offset of the wiki's time zone, from UTC. See $wgLocalTZoffset. 1.13+
        /// </summary>
        public TimeSpan TimeOffset { get; private set; }

        [JsonProperty("timeoffset")]
        private int TimeOffsetProxy
        {
            set { TimeOffset = TimeSpan.FromMinutes(value); }
        }

        /// <summary>
        /// Gets the other extensible site information.
        /// </summary>
        public IReadOnlyDictionary<string, JToken> ExtensionData { get; private set; }

        [JsonExtensionData]
        private IDictionary<string, JToken> ExtensionDataProxy { get; set; }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            ExtensionData = new ReadOnlyDictionary<string, JToken>(ExtensionDataProxy ?? new Dictionary<string, JToken>());
        }

    }

    /// <summary>
    /// Namespace information.
    /// </summary>
    /// <remarks>See https://www.mediawiki.org/wiki/API:Siteinfo#Namespaces .</remarks>
    [JsonObject(MemberSerialization.OptIn)]
    public class NamespaceInfo
    {

        private static IList<string> EmptyStrings = new string[0];

        /// <summary>
        /// An integer identification number which is unique for each namespace.
        /// </summary>
        [JsonProperty]
        public int Id { get; private set; }

        [JsonProperty]
        private string Case
        {
            set
            {
                switch (value)
                {
                    case "case-sensitive":
                        IsCaseSensitive = true;
                        break;
                    case "first-letter":
                        IsCaseSensitive = false;
                        break;
                    default:
                        throw new ArgumentException("Invalid case value.");
                }
            }
        }

        /// <summary>
        /// Whether the first letter in the namespace title is upper-case. (MediaWiki 1.8+)
        /// Note that all the namespace names are case-insensitive. See "remarks" for more information.
        /// </summary>
        /// <remarks>See https://www.mediawiki.org/wiki/API:Siteinfo#Namespaces .</remarks>
        public bool IsCaseSensitive { get; private set; }
        // TODO I'm still not sure what thae "case" property stands for,
        // as all the namespace names are case-insensitive.

        [JsonProperty]
        public bool SubPages { get; private set; }

        /// <summary>
        /// Canonical namespace name.
        /// </summary>
        [JsonProperty("canonical")]
        public string CanonicalName { get; private set; }

        /// <summary>
        /// The displayed name for the namespace. Defined in server LocalSettings.php .
        /// </summary>
        /// <remarks>In JSON, prior to MediaWiki 1.25, the parameter name was *.</remarks>
        [JsonProperty("name")]
        public string CustomName { get; private set; }

        /// <summary>
        /// Namespace alias names.
        /// </summary>
        public IList<string> Aliases { get; private set; } = EmptyStrings;

        // In JSON, prior to MediaWiki 1.25, the parameter name was *.
        [JsonProperty("*")]
        private string StarName
        {
            set { if (CustomName == null) CustomName = value; }
        }

        [JsonProperty("content")]
        public bool IsContent { get; private set; }

        [JsonProperty("nonincludable")]
        public bool IsNonIncludable { get; private set; }

        [JsonProperty]
        public string DefaultContentModel { get; private set; }

        private IList<string> _Aliases;

        internal void AddAlias(string title)
        {
            if (_Aliases == null)
            {
                _Aliases = new List<string>();
                Aliases = new ReadOnlyCollection<string>(_Aliases);
            }
            _Aliases.Add(title);
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            // Try to get canonical name for built-in namespace.
            // This is used especially for NS_MAIN .
            if (CanonicalName == null) CanonicalName = BuiltInNamespaces.GetCanonicalName(Id);
            Debug.Assert(CanonicalName != null);
        }

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        /// <returns>
        /// 表示当前对象的字符串。
        /// </returns>
        public override string ToString()
        {
            return $"[{Id}]{CustomName}" + (Aliases.Count > 0 ? "/" + string.Join("/", Aliases) : null);
        }
    }

    /// <summary>
    /// Provides read-only access to namespace collection.
    /// </summary>
    /// <remarks>Note the namespace name is case-insensitive.</remarks>
    public class NamespaceCollection : ICollection<NamespaceInfo>
    {
        private readonly IDictionary<int, NamespaceInfo> idNsDict;          // id -- ns
        private readonly IDictionary<string, NamespaceInfo> nameNsDict;     // name/custom/alias -- ns

        internal NamespaceCollection(WikiSite site, JObject namespaces, JArray jaliases)
        {
            // jaliases : query.namespacealiases
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (namespaces == null) throw new ArgumentNullException(nameof(namespaces));
            idNsDict = namespaces.ToObject<IDictionary<int, NamespaceInfo>>(Utility.WikiJsonSerializer);
            nameNsDict = idNsDict.Values.ToDictionary(n => n.CanonicalName.ToLowerInvariant());
            // Add custom name.
            foreach (var ns in idNsDict.Values)
            {
                if (ns.CustomName != ns.CanonicalName)
                    nameNsDict.Add(ns.CustomName.ToLowerInvariant(), ns);
            }
            // Add aliases.
            if (jaliases != null)
            {
                foreach (var al in jaliases)
                {
                    var id = (int)al["id"];
                    var name = (string)al["*"];
                    NamespaceInfo ns;
                    if (idNsDict.TryGetValue(id, out ns))
                    {
                        ns.AddAlias(name);
                        var normalizedName = name.ToLowerInvariant();
                        NamespaceInfo varns;
                        if (nameNsDict.TryGetValue(normalizedName, out varns))
                        {
                            // If the namespace alias already exists, check if they're pointing
                            // to the same NamespaceInfo
                            if (varns != ns)
                                site.Logger.LogWarning(
                                    "Namespace alias collision on {Site}: {Name} for {Value1} and {Value2}.",
                                    site, name, ns, varns);
                        }
                        else
                        {
                            nameNsDict.Add(normalizedName, ns);
                        }
                    }
                    else
                    {
                        site.Logger.LogWarning("Cannot find namespace {Id} for alias {Name}.", id, name);
                    }
                }
            }
        }

        /// <summary>
        /// Get the namespace info with specified namespace id.
        /// </summary>
        /// <exception cref="KeyNotFoundException">The specified namespace id cannot be found.</exception>
        public NamespaceInfo this[int index] => idNsDict[index];

        /// <summary>
        /// Get the namespace info with specified namespace name or alias.
        /// </summary>
        /// <exception cref="KeyNotFoundException">The specified namespace id cannot be found.</exception>
        public NamespaceInfo this[string name]
        {
            get
            {
                var ns = TryGetNamespace(name);
                if (ns != null) return ns;
                throw new KeyNotFoundException($"Cannot find namespace for {name} .");
            }
        }

        /// <summary>
        /// Tries to get the namespace info with specified namespace id.
        /// </summary>
        public bool TryGetValue(int id, out NamespaceInfo ns)
        {
            return idNsDict.TryGetValue(id, out ns);
        }

        /// <summary>
        /// Tries to get the namespace info with specified namespace name.
        /// </summary>
        public bool TryGetValue(string name, out NamespaceInfo ns)
        {
            ns = TryGetNamespace(name);
            return ns != null;
        }

        private NamespaceInfo TryGetNamespace(string name)
        {
            NamespaceInfo ns;
            // Namespace name is case-insensitive.
            var nn = Utility.NormalizeTitlePart(name, true).ToLowerInvariant();
            if (nameNsDict.TryGetValue(nn, out ns))
                return ns;
            return null;
        }

        public bool Contains(int index)
        {
            return idNsDict.ContainsKey(index);
        }

        public bool Contains(string name)
        {
            return TryGetNamespace(name) != null;
        }

        #region ICollection

        /// <summary>
        /// 返回一个循环访问集合的枚举器。
        /// </summary>
        /// <returns>
        /// 可用于循环访问集合的 <see cref="T:System.Collections.Generic.IEnumerator`1"/>。
        /// </returns>
        public IEnumerator<NamespaceInfo> GetEnumerator()
        {
            return idNsDict.Values.GetEnumerator();
        }

        /// <summary>
        /// 返回一个循环访问集合的枚举器。
        /// </summary>
        /// <returns>
        /// 可用于循环访问集合的 <see cref="T:System.Collections.IEnumerator"/> 对象。
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// 将某项添加到 <see cref="T:System.Collections.Generic.ICollection`1"/> 中。
        /// </summary>
        /// <param name="item">要添加到 <see cref="T:System.Collections.Generic.ICollection`1"/> 的对象。</param>
        /// <exception cref="T:System.NotSupportedException"><see cref="T:System.Collections.Generic.ICollection`1"/> 为只读。</exception>
        void ICollection<NamespaceInfo>.Add(NamespaceInfo item)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 从 <see cref="T:System.Collections.Generic.ICollection`1"/> 中移除所有项。
        /// </summary>
        /// <exception cref="T:System.NotSupportedException"><see cref="T:System.Collections.Generic.ICollection`1"/> 为只读。</exception>
        void ICollection<NamespaceInfo>.Clear()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 确定 <see cref="T:System.Collections.Generic.ICollection`1"/> 是否包含特定值。
        /// </summary>
        /// <returns>
        /// 如果在 <see cref="T:System.Collections.Generic.ICollection`1"/> 中找到 <paramref name="item"/>，则为 true；否则为 false。
        /// </returns>
        /// <param name="item">要在 <see cref="T:System.Collections.Generic.ICollection`1"/> 中定位的对象。</param>
        bool ICollection<NamespaceInfo>.Contains(NamespaceInfo item)
        {
            return idNsDict.Values.Contains(item);
        }

        /// <summary>
        /// 从特定的 <see cref="T:System.Array"/> 索引开始，将 <see cref="T:System.Collections.Generic.ICollection`1"/> 的元素复制到一个 <see cref="T:System.Array"/> 中。
        /// </summary>
        /// <param name="array">作为从 <see cref="T:System.Collections.Generic.ICollection`1"/> 复制的元素的目标的一维 <see cref="T:System.Array"/>。 <see cref="T:System.Array"/> 必须具有从零开始的索引。</param><param name="arrayIndex"><paramref name="array"/> 中从零开始的索引，从此索引处开始进行复制。</param><exception cref="T:System.ArgumentNullException"><paramref name="array"/> 为 null。</exception><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="arrayIndex"/> 小于 0。</exception><exception cref="T:System.ArgumentException">源 <see cref="T:System.Collections.Generic.ICollection`1"/> 中的元素数目大于从 <paramref name="arrayIndex"/> 到目标 <paramref name="array"/> 末尾之间的可用空间。</exception>
        public void CopyTo(NamespaceInfo[] array, int arrayIndex)
        {
            idNsDict.Values.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// 从 <see cref="T:System.Collections.Generic.ICollection`1"/> 中移除特定对象的第一个匹配项。
        /// </summary>
        /// <returns>
        /// 如果已从 <see cref="T:System.Collections.Generic.ICollection`1"/> 中成功移除 <paramref name="item"/>，则为 true；否则为 false。 如果在原始 <see cref="T:System.Collections.Generic.ICollection`1"/> 中没有找到 <paramref name="item"/>，该方法也会返回 false。
        /// </returns>
        /// <param name="item">要从 <see cref="T:System.Collections.Generic.ICollection`1"/> 中移除的对象。</param><exception cref="T:System.NotSupportedException"><see cref="T:System.Collections.Generic.ICollection`1"/> 为只读。</exception>
        bool ICollection<NamespaceInfo>.Remove(NamespaceInfo item)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 获取 <see cref="T:System.Collections.Generic.ICollection`1"/> 中包含的元素数。
        /// </summary>
        /// <returns>
        /// <see cref="T:System.Collections.Generic.ICollection`1"/> 中包含的元素个数。
        /// </returns>
        public int Count => idNsDict.Count;

        /// <summary>
        /// 获取一个值，该值指示 <see cref="T:System.Collections.Generic.ICollection`1"/> 是否为只读。
        /// </summary>
        /// <returns>
        /// 如果 <see cref="T:System.Collections.Generic.ICollection`1"/> 为只读，则为 true；否则为 false。
        /// </returns>
        bool ICollection<NamespaceInfo>.IsReadOnly => true;

        #endregion
    }

    /// <summary>
    /// An item of interwiki map.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class InterwikiEntry
    {
        private string _Prefix;

        /// <summary>
        /// The prefix of the interwiki link;
        /// this is used the same way as a namespace is used when editing.
        /// </summary>
        /// <remarks>Prefixes must be all lower-case.</remarks>
        [JsonProperty]
        public string Prefix
        {
            get { return _Prefix; }
            private set
            {
                if (value != null) Debug.Assert(value == value.ToLowerInvariant());
                _Prefix = value;
            }
        }

        /// <summary>
        /// Whether the interwiki prefix points to a site belonging to the current wiki farm.
        /// The value is read straight out of the iw_local column of the interwiki table. 
        /// </summary>
        [JsonProperty]
        public bool IsLocal { get; private set; }

        /// <summary>
        /// Whether transcluding pages from this wiki is allowed.
        /// Note that this has no effect unless crosswiki transclusion is enabled on the current wiki.
        /// </summary>
        [JsonProperty("trans")]
        public bool AllowsTransclusion { get; private set; }

        /// <summary>
        /// Autonym of the language, if the interwiki prefix is a language code
        /// defined in Language::fetchLanguageNames() from $wgExtraLanguageNames,
        /// </summary>
        [JsonProperty("language")]
        public string LanguageAutonym { get; private set; }

        /// <summary>
        /// The URL of the wiki, with "$1" as a placeholder for an article name.
        /// </summary>
        [JsonProperty]
        public string Url { get; private set; }

        /// <summary>
        /// Whether the value of URL can be treated as protocol-relative. (MediaWiki 1.24+)
        /// Note, however, that the <see cref="Url"/> actually returned will always include the current protocol.
        /// </summary>
        [JsonProperty("protorel")]
        public bool IsProtocolRelative { get; private set; }

        /// <summary>
        /// Whether the interwiki link points to the current wiki, based on Manual:$wgLocalInterwikis.
        /// </summary>
        [JsonProperty]
        public bool IsLocalInterwiki { get; private set; }

        /// <summary>
        /// If the interwiki prefix is an extra language link,
        /// this will contain the friendly site name used in the tooltip text of the links. (MediaWiki 1.24+)
        /// </summary>
        [JsonProperty]
        public string SiteName { get; private set; }

        /// <summary>
        /// The internal name of the database. Not filled in by default;
        /// it may be missing for you. The value is read straight out
        /// of the iw_wikiid column of the interwiki table.
        /// </summary>
        [JsonProperty]
        public string WikiId { get; set; }

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        /// <returns>
        /// 表示当前对象的字符串。
        /// </returns>
        public override string ToString()
        {
            return Prefix + ":" + LanguageAutonym;
        }
    }

    /// <summary>
    /// Provides read-only access to interwiki map.
    /// </summary>
    public class InterwikiMap : ICollection<InterwikiEntry>
    {
        private readonly IDictionary<string, InterwikiEntry> nameIwDict;

        internal InterwikiMap(WikiSite site, JArray interwikiMap, ILogger logger)
        {
            // interwikiMap : query.namespacealiases
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (interwikiMap == null) throw new ArgumentNullException(nameof(interwikiMap));
            var entries = interwikiMap.ToObject<IList<InterwikiEntry>>(Utility.WikiJsonSerializer);
            var entryDict = new Dictionary<string, InterwikiEntry>(entries.Count);
            foreach (var entry in entries)
            {
                var prefix = entry.Prefix.ToLowerInvariant();
                try
                {
                    entryDict.Add(prefix, entry);
                }
                catch (ArgumentException)
                {
                    // Duplicate key. We will just keep the first occurence.
                    if (entryDict[prefix].Url != entry.Url)
                    {
                        // And there are same prefixes assigned to different URLs. Worse.
                        logger.LogWarning("Detected conflicting interwiki URL for prefix {Prefix}.", prefix);
                    }
                }
            }
            nameIwDict = entryDict;
        }

        /// <summary>
        /// Get the interwiki entry with specified prefix. The match is case-insensitive.
        /// </summary>
        /// <exception cref="KeyNotFoundException">The specified prefix cannot be found.</exception>
        public InterwikiEntry this[string prefix] => nameIwDict[prefix];

        /// <summary>
        /// Determines whether there's an interwiki entry with specified prefix.
        /// The match is case-insensitive, and name will internally be normalized.
        /// </summary>
        public bool Contains(string name)
        {
            name = name.ToLowerInvariant().Trim(' ', '_');
            return nameIwDict.ContainsKey(name);
        }

        #region ICollection

        /// <summary>
        /// 返回一个循环访问集合的枚举器。
        /// </summary>
        /// <returns>
        /// 可用于循环访问集合的 <see cref="T:System.Collections.Generic.IEnumerator`1"/>。
        /// </returns>
        public IEnumerator<InterwikiEntry> GetEnumerator()
        {
            return nameIwDict.Values.GetEnumerator();
        }

        /// <summary>
        /// 返回一个循环访问集合的枚举器。
        /// </summary>
        /// <returns>
        /// 可用于循环访问集合的 <see cref="T:System.Collections.IEnumerator"/> 对象。
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// 将某项添加到 <see cref="T:System.Collections.Generic.ICollection`1"/> 中。
        /// </summary>
        /// <param name="item">要添加到 <see cref="T:System.Collections.Generic.ICollection`1"/> 的对象。</param>
        /// <exception cref="T:System.NotSupportedException"><see cref="T:System.Collections.Generic.ICollection`1"/> 为只读。</exception>
        void ICollection<InterwikiEntry>.Add(InterwikiEntry item)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 从 <see cref="T:System.Collections.Generic.ICollection`1"/> 中移除所有项。
        /// </summary>
        /// <exception cref="T:System.NotSupportedException"><see cref="T:System.Collections.Generic.ICollection`1"/> 为只读。</exception>
        void ICollection<InterwikiEntry>.Clear()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 确定 <see cref="T:System.Collections.Generic.ICollection`1"/> 是否包含特定值。
        /// </summary>
        /// <returns>
        /// 如果在 <see cref="T:System.Collections.Generic.ICollection`1"/> 中找到 <paramref name="item"/>，则为 true；否则为 false。
        /// </returns>
        /// <param name="item">要在 <see cref="T:System.Collections.Generic.ICollection`1"/> 中定位的对象。</param>
        bool ICollection<InterwikiEntry>.Contains(InterwikiEntry item)
        {
            return nameIwDict.Values.Contains(item);
        }

        /// <summary>
        /// 从特定的 <see cref="T:System.Array"/> 索引开始，将 <see cref="T:System.Collections.Generic.ICollection`1"/> 的元素复制到一个 <see cref="T:System.Array"/> 中。
        /// </summary>
        /// <param name="array">作为从 <see cref="T:System.Collections.Generic.ICollection`1"/> 复制的元素的目标的一维 <see cref="T:System.Array"/>。 <see cref="T:System.Array"/> 必须具有从零开始的索引。</param><param name="arrayIndex"><paramref name="array"/> 中从零开始的索引，从此索引处开始进行复制。</param><exception cref="T:System.ArgumentNullException"><paramref name="array"/> 为 null。</exception><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="arrayIndex"/> 小于 0。</exception><exception cref="T:System.ArgumentException">源 <see cref="T:System.Collections.Generic.ICollection`1"/> 中的元素数目大于从 <paramref name="arrayIndex"/> 到目标 <paramref name="array"/> 末尾之间的可用空间。</exception>
        public void CopyTo(InterwikiEntry[] array, int arrayIndex)
        {
            nameIwDict.Values.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// 从 <see cref="T:System.Collections.Generic.ICollection`1"/> 中移除特定对象的第一个匹配项。
        /// </summary>
        /// <returns>
        /// 如果已从 <see cref="T:System.Collections.Generic.ICollection`1"/> 中成功移除 <paramref name="item"/>，则为 true；否则为 false。 如果在原始 <see cref="T:System.Collections.Generic.ICollection`1"/> 中没有找到 <paramref name="item"/>，该方法也会返回 false。
        /// </returns>
        /// <param name="item">要从 <see cref="T:System.Collections.Generic.ICollection`1"/> 中移除的对象。</param><exception cref="T:System.NotSupportedException"><see cref="T:System.Collections.Generic.ICollection`1"/> 为只读。</exception>
        bool ICollection<InterwikiEntry>.Remove(InterwikiEntry item)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 获取 <see cref="T:System.Collections.Generic.ICollection`1"/> 中包含的元素数。
        /// </summary>
        /// <returns>
        /// <see cref="T:System.Collections.Generic.ICollection`1"/> 中包含的元素个数。
        /// </returns>
        public int Count => nameIwDict.Count;

        /// <summary>
        /// 获取一个值，该值指示 <see cref="T:System.Collections.Generic.ICollection`1"/> 是否为只读。
        /// </summary>
        /// <returns>
        /// 如果 <see cref="T:System.Collections.Generic.ICollection`1"/> 为只读，则为 true；否则为 false。
        /// </returns>
        bool ICollection<InterwikiEntry>.IsReadOnly => true;

        #endregion
    }

    /// <summary>
    /// Provides read-only access to extension collection.
    /// </summary>
    public class ExtensionCollection : ICollection<ExtensionInfo>
    {
        private readonly IList<ExtensionInfo> extensions;
        private readonly ILookup<string, ExtensionInfo> nameLookup;

        internal ExtensionCollection(WikiSite site, JArray jextensions)
        {
            // extensions : query.extensions
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (jextensions == null) throw new ArgumentNullException(nameof(jextensions));
            extensions = jextensions.ToObject<IList<ExtensionInfo>>(Utility.WikiJsonSerializer);
            nameLookup = extensions.ToLookup(e => e.Name);
        }

        /// <summary>
        /// Get the extensions with specified name. The match is case-sensitive.
        /// </summary>
        public IEnumerable<ExtensionInfo> this[string name] => nameLookup[name];

        /// <summary>
        /// Determines whether there's an extensions with specified name.
        /// The match is case-sensitive.
        /// </summary>
        public bool Contains(string name)
        {
            return nameLookup[name].Any();
        }

        #region ICollection

        /// <summary>
        /// 返回一个循环访问集合的枚举器。
        /// </summary>
        /// <returns>
        /// 可用于循环访问集合的 <see cref="T:System.Collections.Generic.IEnumerator`1"/>。
        /// </returns>
        public IEnumerator<ExtensionInfo> GetEnumerator()
        {
            return extensions.GetEnumerator();
        }

        /// <summary>
        /// 返回一个循环访问集合的枚举器。
        /// </summary>
        /// <returns>
        /// 可用于循环访问集合的 <see cref="T:System.Collections.IEnumerator"/> 对象。
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// 将某项添加到 <see cref="T:System.Collections.Generic.ICollection`1"/> 中。
        /// </summary>
        /// <param name="item">要添加到 <see cref="T:System.Collections.Generic.ICollection`1"/> 的对象。</param>
        /// <exception cref="T:System.NotSupportedException"><see cref="T:System.Collections.Generic.ICollection`1"/> 为只读。</exception>
        void ICollection<ExtensionInfo>.Add(ExtensionInfo item)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 从 <see cref="T:System.Collections.Generic.ICollection`1"/> 中移除所有项。
        /// </summary>
        /// <exception cref="T:System.NotSupportedException"><see cref="T:System.Collections.Generic.ICollection`1"/> 为只读。</exception>
        void ICollection<ExtensionInfo>.Clear()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 确定 <see cref="T:System.Collections.Generic.ICollection`1"/> 是否包含特定值。
        /// </summary>
        /// <returns>
        /// 如果在 <see cref="T:System.Collections.Generic.ICollection`1"/> 中找到 <paramref name="item"/>，则为 true；否则为 false。
        /// </returns>
        /// <param name="item">要在 <see cref="T:System.Collections.Generic.ICollection`1"/> 中定位的对象。</param>
        bool ICollection<ExtensionInfo>.Contains(ExtensionInfo item)
        {
            return extensions.Contains(item);
        }

        /// <summary>
        /// 从特定的 <see cref="T:System.Array"/> 索引开始，将 <see cref="T:System.Collections.Generic.ICollection`1"/> 的元素复制到一个 <see cref="T:System.Array"/> 中。
        /// </summary>
        /// <param name="array">作为从 <see cref="T:System.Collections.Generic.ICollection`1"/> 复制的元素的目标的一维 <see cref="T:System.Array"/>。 <see cref="T:System.Array"/> 必须具有从零开始的索引。</param><param name="arrayIndex"><paramref name="array"/> 中从零开始的索引，从此索引处开始进行复制。</param><exception cref="T:System.ArgumentNullException"><paramref name="array"/> 为 null。</exception><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="arrayIndex"/> 小于 0。</exception><exception cref="T:System.ArgumentException">源 <see cref="T:System.Collections.Generic.ICollection`1"/> 中的元素数目大于从 <paramref name="arrayIndex"/> 到目标 <paramref name="array"/> 末尾之间的可用空间。</exception>
        public void CopyTo(ExtensionInfo[] array, int arrayIndex)
        {
            extensions.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// 从 <see cref="T:System.Collections.Generic.ICollection`1"/> 中移除特定对象的第一个匹配项。
        /// </summary>
        /// <returns>
        /// 如果已从 <see cref="T:System.Collections.Generic.ICollection`1"/> 中成功移除 <paramref name="item"/>，则为 true；否则为 false。 如果在原始 <see cref="T:System.Collections.Generic.ICollection`1"/> 中没有找到 <paramref name="item"/>，该方法也会返回 false。
        /// </returns>
        /// <param name="item">要从 <see cref="T:System.Collections.Generic.ICollection`1"/> 中移除的对象。</param><exception cref="T:System.NotSupportedException"><see cref="T:System.Collections.Generic.ICollection`1"/> 为只读。</exception>
        bool ICollection<ExtensionInfo>.Remove(ExtensionInfo item)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 获取 <see cref="T:System.Collections.Generic.ICollection`1"/> 中包含的元素数。
        /// </summary>
        /// <returns>
        /// <see cref="T:System.Collections.Generic.ICollection`1"/> 中包含的元素个数。
        /// </returns>
        public int Count => extensions.Count;

        /// <summary>
        /// 获取一个值，该值指示 <see cref="T:System.Collections.Generic.ICollection`1"/> 是否为只读。
        /// </summary>
        /// <returns>
        /// 如果 <see cref="T:System.Collections.Generic.ICollection`1"/> 为只读，则为 true；否则为 false。
        /// </returns>
        bool ICollection<ExtensionInfo>.IsReadOnly => true;

        #endregion
    }
    [JsonObject(MemberSerialization.OptIn)]
    public class ExtensionInfo
    {
        [JsonProperty]
        public string Type { get; private set; }

        [JsonProperty]
        public string Name { get; private set; }

        [JsonProperty("namemsg")]
        public string NameMessage { get; private set; }

        [JsonProperty]
        public string Description { get; private set; }

        [JsonProperty("descriptionmsg")]
        public string DescriptionMessage { get; private set; }

        [JsonProperty]
        public string Author { get; private set; }

        [JsonProperty]
        public string Url { get; private set; }

        [JsonProperty("vcs-system")]
        public string VcsSystem { get; private set; }

        [JsonProperty("vcs-version")]
        public string VcsVersion { get; private set; }

        [JsonProperty("vcs-url")]
        public string VcsUrl { get; private set; }

        [JsonProperty("vcs-date")]
        public string VcsDate { get; private set; }

        [JsonProperty("license-name")]
        public string LicenseName { get; private set; }

        [JsonProperty]
        public string License { get; private set; }

        [JsonProperty]
        public string Version { get; private set; }

        [JsonProperty]
        public string Credits { get; private set; }

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        /// <returns>
        /// 表示当前对象的字符串。
        /// </returns>
        public override string ToString()
        {
            return $"{Type}: {Name} - {Version}";
        }
    }

    /// <summary>
    /// Contains statistical information of a MedaiWiki site.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class SiteStatistics
    {
        [JsonProperty("pages")]
        public int PagesCount { get; private set; }

        [JsonProperty("articles")]
        public int ArticlesCount { get; private set; }

        [JsonProperty("edits")]
        public int EditsCount { get; private set; }

        /// <summary>
        /// Count of uploaded files.
        /// </summary>
        /// <remarks>
        /// For historical reasons, the number of files on the wiki is labelled as images not files.
        /// This number refers to uploaded files of all types, not just images.
        /// </remarks>
        [JsonProperty("images")]
        public int FilesCount { get; private set; }

        [JsonProperty("users")]
        public int UsersCount { get; private set; }

        [JsonProperty("activeusers")]
        public int ActiveUsersCount { get; private set; }

        [JsonProperty("admins")]
        public int AdministratorsCount { get; private set; }

        [JsonProperty("jobs")]
        public int JobsCount { get; private set; }

        [JsonProperty("queued-massmessages")]
        public int MassMessageQueueLength { get; private set; }
    }

}
