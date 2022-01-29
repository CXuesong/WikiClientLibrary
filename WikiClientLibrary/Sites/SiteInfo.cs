using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Sites
{
    /// <summary>
    /// Provides read-only access to general site information.
    /// </summary>
    /// <seealso cref="WikiSite"/>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class SiteInfo
    {

        private string _Generator;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
        public SiteInfo()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
        {
        }

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
        /// API version information as found in <c>$wgVersion</c>. (MW 1.8+)
        /// </summary>
        /// <remarks>Example value: <c>MediaWiki 1.28.0-wmf.15</c>.</remarks>
        [JsonProperty("generator")]
        public string Generator
        {
            get { return _Generator; }
            private set
            {
                _Generator = value;
                if (value == null) return;
                var pos = 0;
                NEXT:
                pos = value.IndexOf(' ', pos) + 1;
                if (pos <= 0 || pos >= value.Length)
                {
                    Version = MediaWikiVersion.Zero;
                    return;
                }
                if (value[pos] < '0' && value[pos] > '9')
                    goto NEXT;
                Version = MediaWikiVersion.Parse(value[pos..], true);
            }
        }

        /// <summary>
        /// Gets MediaWiki API version.
        /// </summary>
        /// <remarks>
        /// <para>This version is parsed from the value of <see cref="Generator"/>.
        /// Suffix truncation is allowed when parsing the version.
        /// If WCL failed to extract version part from <see cref="Generator"/>, this property will be <see cref="MediaWikiVersion.Zero"/>.</para>
        /// <para>See <see cref="MediaWikiVersion.Parse(string,bool)"/> for more information about version suffix truncation.</para>
        /// </remarks>
        public MediaWikiVersion Version { get; private set; }

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
                IsTitleCaseSensitive = value switch
                {
                    "case-sensitive" => true,
                    "first-letter" => false,
                    _ => throw new ArgumentException("Invalid case value.")
                };
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

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
        public NamespaceInfo()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
        {
        }

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
                IsCaseSensitive = value switch
                {
                    "case-sensitive" => true,
                    "first-letter" => false,
                    _ => throw new ArgumentException("Invalid case value.")
                };
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
        public IList<string> Aliases { get; private set; } = Array.Empty<string>();

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

        private IList<string>? _Aliases;

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
            if (CanonicalName == null) CanonicalName = BuiltInNamespaces.GetCanonicalName(Id)!;
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
        private readonly Dictionary<int, NamespaceInfo> idNsDict;           // id -- ns
        private readonly Dictionary<string, NamespaceInfo> nameNsDict;      // name/custom/alias -- ns

        internal NamespaceCollection(WikiSite site, JObject namespaces, JArray jaliases)
        {
            // jaliases : query.namespacealiases
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (namespaces == null) throw new ArgumentNullException(nameof(namespaces));
            idNsDict = namespaces.ToObject<Dictionary<int, NamespaceInfo>>(Utility.WikiJsonSerializer);
            // Not using StringComparer.InvariantCultureIgnoreCase since we will perform normalization during entry queries.
            nameNsDict = new Dictionary<string, NamespaceInfo>();
            foreach (var value in idNsDict.Values)
            {
                var normalizedName = value.CanonicalName.ToUpperInvariant();
                if (!nameNsDict.TryAdd(normalizedName, value))
                {
                    site.Logger.LogWarning(
                        "Namespace canonical name collision on {Site}: {Name} for {Value1} and {Value2}.",
                        site, value.CanonicalName, value, nameNsDict[normalizedName]);
                }
            }
            // Add custom name.
            foreach (var ns in idNsDict.Values)
            {
                if (ns.CustomName != ns.CanonicalName)
                    nameNsDict.Add(ns.CustomName.ToUpperInvariant(), ns);
            }
            // Add aliases.
            if (jaliases != null)
            {
                foreach (var al in jaliases)
                {
                    var id = (int)al["id"];
                    var name = (string)al["*"];
                    if (idNsDict.TryGetValue(id, out var ns))
                    {
                        ns.AddAlias(name);
                        var normalizedName = name.ToUpperInvariant();
                        if (!nameNsDict.TryAdd(normalizedName, ns))
                        {
                            // If the namespace alias already exists, check if they're pointing
                            // to the same NamespaceInfo
                            var existingNs = nameNsDict[normalizedName];
                            if (existingNs != ns)
                                site.Logger.LogWarning(
                                    "Namespace alias collision on {Site}: {Name} for {Value1} and {Value2}.",
                                    site, name, ns, existingNs);
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
        public bool TryGetValue(int id, [NotNullWhen(true)] out NamespaceInfo? ns)
        {
            return idNsDict.TryGetValue(id, out ns);
        }

        /// <summary>
        /// Tries to get the namespace info with specified namespace name.
        /// </summary>
        public bool TryGetValue(string name, [NotNullWhen(true)] out NamespaceInfo? ns)
        {
            ns = TryGetNamespace(name);
            return ns != null;
        }

        private NamespaceInfo? TryGetNamespace(string name)
        {
            // Namespace name is case-insensitive.
            // c.f. https://docs.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1308
            var nn = Utility.NormalizeTitlePart(name, true).ToUpperInvariant();
            return nameNsDict.TryGetValue(nn, out var ns) ? ns : null;
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

        /// <inheritdoc />
        public IEnumerator<NamespaceInfo> GetEnumerator()
        {
            return idNsDict.Values.GetEnumerator();
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <inheritdoc />
        void ICollection<NamespaceInfo>.Add(NamespaceInfo item)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        void ICollection<NamespaceInfo>.Clear()
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        bool ICollection<NamespaceInfo>.Contains(NamespaceInfo item)
        {
            return idNsDict.Values.Contains(item);
        }

        /// <inheritdoc />
        public void CopyTo(NamespaceInfo[] array, int arrayIndex)
        {
            idNsDict.Values.CopyTo(array, arrayIndex);
        }

        /// <inheritdoc />
        bool ICollection<NamespaceInfo>.Remove(NamespaceInfo item)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public int Count => idNsDict.Count;

        /// <inheritdoc />
        bool ICollection<NamespaceInfo>.IsReadOnly => true;

#endregion
    }

    /// <summary>
    /// An item of interwiki map.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class InterwikiEntry
    {

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
        public InterwikiEntry()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
        {
        }

        /// <summary>
        /// The prefix of the interwiki link;
        /// this is used the same way as a namespace is used when editing.
        /// </summary>
        /// <remarks>
        /// Prefixes must be all lower-case.
        /// See <a href="https://www.mediawiki.org/wiki/Manual:Interwiki#Field_documentation">mw:Manual:Interwiki#Field documentation</a> for more information.
        /// </remarks>
        [JsonProperty]
        public string Prefix { get; private set; }

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

        /// <inheritdoc />
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
            // Not using StringComparer.InvariantCultureIgnoreCase since we will perform normalization during entry queries.
            var entryDict = new Dictionary<string, InterwikiEntry>(entries.Count);
            foreach (var entry in entries)
            {
                var prefix = entry.Prefix.ToUpperInvariant();
                if (prefix != entry.Prefix)
                {
                    // c.f. https://www.mediawiki.org/wiki/Manual:Interwiki#Field_documentation
                    logger.LogWarning("Detected non-compliant Interwiki prefix {Prefix}. Interwiki prefix must be all lower-case.", entry.Prefix);
                }
                if (!entryDict.TryAdd(prefix, entry))
                {
                    // Duplicate key. We will just keep the first occurrence.
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
        public InterwikiEntry this[string prefix]
            => nameIwDict[Utility.NormalizeTitlePart(prefix, false).ToUpperInvariant()];

        /// <summary>
        /// Determines whether there's an interwiki entry with specified prefix.
        /// The match is case-insensitive, and name will internally be normalized.
        /// </summary>
        public bool Contains(string name)
        {
            name = Utility.NormalizeTitlePart(name, false).ToUpperInvariant();
            return nameIwDict.ContainsKey(name);
        }

#region ICollection

        /// <inheritdoc />
        public IEnumerator<InterwikiEntry> GetEnumerator()
        {
            return nameIwDict.Values.GetEnumerator();
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <inheritdoc />
        void ICollection<InterwikiEntry>.Add(InterwikiEntry item)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        void ICollection<InterwikiEntry>.Clear()
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        bool ICollection<InterwikiEntry>.Contains(InterwikiEntry item)
        {
            return nameIwDict.Values.Contains(item);
        }

        /// <inheritdoc />
        public void CopyTo(InterwikiEntry[] array, int arrayIndex)
        {
            nameIwDict.Values.CopyTo(array, arrayIndex);
        }

        /// <inheritdoc />
        bool ICollection<InterwikiEntry>.Remove(InterwikiEntry item)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public int Count => nameIwDict.Count;

        /// <inheritdoc />
        bool ICollection<InterwikiEntry>.IsReadOnly => true;

#endregion
    }

    /// <summary>
    /// Provides read-only access to extension collection.
    /// </summary>
    public class ExtensionCollection : ReadOnlyCollection<ExtensionInfo>
    {
        private readonly ILookup<string, ExtensionInfo> nameLookup;

        internal ExtensionCollection(WikiSite site, JArray jextensions)
            : base(jextensions.ToObject<IList<ExtensionInfo>>(Utility.WikiJsonSerializer))
        {
            // extensions : query.extensions
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (jextensions == null) throw new ArgumentNullException(nameof(jextensions));
            nameLookup = this.Items.ToLookup(e => e.Name);
        }

        /// <summary>
        /// Gets the extension with specified name. The match is case-sensitive.
        /// </summary>
        /// <param name="name">extension name to look for.</param>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is <c>null</c>.</exception>
        /// <exception cref="KeyNotFoundException">there is no matching extension in the collection.</exception>
        /// <see cref="TryGet"/>
        public ExtensionInfo this[string name]
        {
            get
            {
                var match = TryGet(name);
                if (match == null)
                    throw new KeyNotFoundException(string.Format(Prompts.ExceptionExtensionNotFound1, name));
                return match;
            }
        }

        /// <summary>
        /// Tries to get the extension with specified name. The match is case-sensitive.
        /// </summary>
        /// <param name="name">extension name to look for.</param>
        /// <returns>extension information, if available; or <c>null</c> if the extension is not found.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is <c>null</c>.</exception>
        public ExtensionInfo? TryGet(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            return nameLookup[name].FirstOrDefault();
        }

        /// <summary>
        /// Determines whether there's an extensions with specified name.
        /// The match is case-sensitive.
        /// </summary>
        public bool Contains(string name) => nameLookup[name].Any();

    }

    [JsonObject(MemberSerialization.OptIn)]
    public class ExtensionInfo
    {

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
        public ExtensionInfo()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
        {
        }

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

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{Type}: {Name} - {Version}";
        }

    }

    /// <summary>
    /// Contains statistical information of a MediaWiki site.
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

    /// <summary>
    /// Contains information for an available magic word on MediaWiki site.
    /// </summary>
    /// <remarks>
    /// See <a href="https://www.mediawiki.org/wiki/Manual:Magic_words">mw:Manual:Magic words</a>.
    /// </remarks>
    [JsonObject(MemberSerialization.OptIn)]
    public class MagicWordInfo
    {
        /// <summary>Name of the magic word. This is a case-sensitive magic word ID.</summary>
        [JsonProperty]
        public string Name { get; private set; } = "";

        /// <summary>Aliases of the magic word. These are the valid wikitext expression when magic word is to be invoked.</summary>
        [JsonProperty]
        public IReadOnlyCollection<string> Aliases { get; private set; } = ImmutableList<string>.Empty;

        /// <summary>Whether the magic word aliases are case-sensitive.</summary>
        /// <remarks>The value of this property affects the behavior of <see cref="MagicWordCollection.TryGetByAlias(string)"/></remarks>
        [JsonProperty("case-sensitive")]
        public bool CaseSensitive { get; private set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return Aliases.Count == 0 ? Name : $"{Name} ({string.Join(',', Aliases)})";
        }
    }

    /// <summary>
    /// Provides read-only access to MediaWiki magic words collection.
    /// </summary>
    /// <remarks>
    /// See <a href="https://www.mediawiki.org/wiki/Manual:Magic_words">mw:Manual:Magic words</a>.
    /// </remarks>
    public class MagicWordCollection : ReadOnlyCollection<MagicWordInfo>
    {

        private readonly ILookup<string, MagicWordInfo> magicWordLookup;
        private readonly ILookup<string, MagicWordInfo> magicWordAliasLookup;

        /// <inheritdoc />
        internal MagicWordCollection(JArray jMagicWords)
            : base(jMagicWords.ToObject<IList<MagicWordInfo>>(Utility.WikiJsonSerializer))
        {
            this.magicWordLookup = this.Items.ToLookup(i => i.Name);
            this.magicWordAliasLookup = this.Items
                .SelectMany(i => i.Aliases.Select(a => (Alias: a, Item: i)))
                .ToLookup(p => p.Alias, p => p.Item, StringComparer.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Gets the magic word with specified name (magic word ID). The match is case-sensitive.
        /// </summary>
        /// <param name="name">name of the magic word (magic word ID).</param>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is <c>null</c>.</exception>
        /// <exception cref="KeyNotFoundException">there is no matching magic word in the collection.</exception>
        /// <remarks>To look for a magic word by its alias (wikitext), use <see cref="TryGetByAlias"/>.</remarks>
        /// <see cref="TryGet"/>
        /// <see cref="TryGetByAlias"/>
        public MagicWordInfo this[string name]
        {
            get
            {
                var match = TryGet(name);
                if (match == null)
                    throw new KeyNotFoundException(string.Format(Prompts.ExceptionMagicWordNotFound1, name));
                return match;
            }
        }

        /// <summary>
        /// Determines whether there's a magic word with specified name (magic word ID). The match is case-sensitive.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is <c>null</c>.</exception>
        public bool ContainsName(string name) => TryGet(name) != null;

        /// <summary>
        /// Determines whether there's a magic word with specified alias (wikitext).
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="alias"/> is <c>null</c>.</exception>
        public bool ContainsAlias(string alias) => TryGetByAlias(alias) != null;

        /// <summary>
        /// Tries to lookup a magic word by name (magic word ID) in the collection. The match is case-sensitive.
        /// </summary>
        /// <param name="name">the magic word name.</param>
        /// <returns>a magic word entry, or <c>null</c> if a matching magic word cannot be found.</returns>
        /// <remarks>To look for a magic word by its alias (wikitext), use <see cref="TryGetByAlias"/>.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is <c>null</c>.</exception>
        public MagicWordInfo? TryGet(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            return magicWordLookup[name].FirstOrDefault();
        }

        /// <summary>
        /// Tries to lookup a magic word by alias (wikitext) in the collection.
        /// </summary>
        /// <param name="alias">the magic word alias.</param>
        /// <returns>a magic word entry, or <c>null</c> if a matching magic word cannot be found.</returns>
        /// <remarks>
        /// <para>This function checks <see cref="MagicWordInfo.CaseSensitive"/> for case-sensitivity during lookup.</para>
        /// <para>To look for a magic word by its magic word ID, use <see cref="TryGet"/> or <see cref="this[string]"/>.</para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="alias"/> is <c>null</c>.</exception>
        public MagicWordInfo? TryGetByAlias(string alias)
        {
            if (alias == null) throw new ArgumentNullException(nameof(alias));
            return magicWordAliasLookup[alias].FirstOrDefault(i => i.Aliases.Any(a =>
                string.Equals(a, alias, i.CaseSensitive ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase)
            ));
        }
    }

}
