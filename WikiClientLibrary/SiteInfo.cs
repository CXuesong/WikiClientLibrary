using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Specialized;
using System.Runtime.Serialization;

namespace WikiClientLibrary
{
    /// <summary>
    /// Provides read-only access to site information.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class SiteInfo
    {
        private string _Generator;

        /// <summary>
        /// The title of the main page, as found in MediaWiki:Mainpage. 1.8+
        /// </summary>
        [JsonProperty] // This should be kept or private setter will be ignored by Newtonsoft.JSON.
        public string MainPage { get; private set; }

        /// <summary>
        /// The absolute path to the main page. 1.8+
        /// </summary>
        [JsonProperty("base")]
        public string BaseUrl { get; private set; }

        /// <summary>
        /// The absolute or protocol-relative base URL for the server. See $wgServer. 1.16+
        /// </summary>
        [JsonProperty("server")]
        public string ServerUrl { get; private set; }

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

        [JsonProperty]
        public long MaxUploadSize { get; private set; }

        [JsonProperty]
        public int MinUploadChunkSize { get; private set; }

        /// <summary>
        /// The path of index.php relative to the document root. 
        /// </summary>
        [JsonProperty("script")]
        public string ScriptFilePath { get; private set; }

        /// <summary>
        /// The base URL path relative to the document root. 
        /// </summary>
        [JsonProperty("scriptpath")]
        public string ScriptDirectoryPath { get; private set; }

        [JsonProperty("favicon")]
        public string FavIconUrl { get; private set; }

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
        /// Whether the first letter in the namespace title is case-sensitive. (MediaWiki 1.8+)
        /// </summary>
        public bool IsCaseSensitive { get; private set; }

        [JsonProperty]
        public bool SubPages { get; private set; }

        /// <summary>
        /// Canonical namespace name.
        /// </summary>
        /// <remarks>In JSON, prior to MediaWiki 1.25, the parameter name was *.</remarks>
        [JsonProperty("canonical")]
        public string Name { get; private set; }

        /// <summary>
        /// Namespace alias names.
        /// </summary>
        public IList<string> Aliases { get; private set; } = EmptyStrings;

        // In JSON, prior to MediaWiki 1.25, the parameter name was *.
        [JsonProperty("*")]
        private string StarName
        {
            set { if (Name == null) Name = value; }
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

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        /// <returns>
        /// 表示当前对象的字符串。
        /// </returns>
        public override string ToString()
        {
            return $"[{Id}]{Name}" + (Aliases.Count > 0 ? "/" + string.Join("/", Aliases) : null);
        }
    }

    /// <summary>
    /// Provides read-only access to namespace collection.
    /// </summary>
    public class NamespaceCollection : ICollection<NamespaceInfo>
    {
        private readonly IDictionary<int, NamespaceInfo> idNsDict;
        private readonly IDictionary<string, NamespaceInfo> nameNsDict;

        internal NamespaceCollection(Site site, JObject namespaces, JArray jaliases)
        {
            // jaliases : query.namespacealiases
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (namespaces == null) throw new ArgumentNullException(nameof(namespaces));
            idNsDict = namespaces.ToObject<IDictionary<int, NamespaceInfo>>(Utility.WikiJsonSerializer);
            nameNsDict = idNsDict.Values.ToDictionary(n => n.Name);
            if (jaliases != null)
            {
                foreach (var al in jaliases)
                {
                    var id = (int) al["id"];
                    var name = (string) al["*"];
                    NamespaceInfo ns;
                    if (idNsDict.TryGetValue(id, out ns))
                    {
                        ns.AddAlias(name);
                        nameNsDict.Add(Utility.NormalizeTitlePart(name, ns.IsCaseSensitive), ns);
                    }
                    else
                    {
                        site.Logger?.Warn($"Cannot find namespace {id} for alias {name} .");
                    }
                }
            }
        }

        /// <summary>
        /// Get the namespace info with specified nemspace id.
        /// </summary>
        /// <exception cref="KeyNotFoundException">The specified namespace id cannot be found.</exception>
        public NamespaceInfo this[int index] => idNsDict[index];

        /// <summary>
        /// Get the namespace info with specified nemspace name or alias.
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

        private NamespaceInfo TryGetNamespace(string name)
        {
            NamespaceInfo ns;
            // Try the case-sensitive one first.
            if (nameNsDict.TryGetValue(Utility.NormalizeTitlePart(name, true), out ns))
                return ns;
            // Try case-insensitive.
            if (nameNsDict.TryGetValue(Utility.NormalizeTitlePart(name, false), out ns))
            {
                if (!ns.IsCaseSensitive) return ns;
            }
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
    /// An item of iterwiki map.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class InterwikiEntry
    {
        /// <summary>
        /// The prefix of the interwiki link;
        /// this is used the same way as a namespace is used when editing.
        /// </summary>
        /// <remarks>Prefixes must be all lower-case.</remarks>
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
        /// Whether the value of url can be treated as protocol-relative. (MediaWiki 1.24+)
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

        internal InterwikiMap(Site site, JArray interwikiMap)
        {
            // interwikiMap : query.namespacealiases
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (interwikiMap == null) throw new ArgumentNullException(nameof(interwikiMap));
            // I should use InvariantIgnoreCase. But there's no such a member in PCL.
            nameIwDict = interwikiMap.ToObject<IList<InterwikiEntry>>(Utility.WikiJsonSerializer)
                .ToDictionary(e => e.Prefix, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Get the interwiki entry with specified prefix. The match is case-insensitive.
        /// </summary>
        /// <exception cref="KeyNotFoundException">The specified prefix cannot be found.</exception>
        public InterwikiEntry this[string name] => nameIwDict[name];

        public bool Contains(string name)
        {
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
}
