using System;
using System.Collections;
using System.Collections.Generic;
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
        [JsonProperty]      // This should be kept or private setter will be ignored by Newtonsoft.JSON.
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
                    var part = value.Split(new[] {' ', '-'});
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
    }

    /// <summary>
    /// Namespace information.
    /// </summary>
    /// <remarks>See https://www.mediawiki.org/wiki/API:Siteinfo#Namespaces .</remarks>
    [JsonObject(MemberSerialization.OptIn)]
    public class NamespaceInfo
    {
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

        public bool IsCaseSensitive { get; private set; }

        [JsonProperty] 
        public bool SubPages { get; private set; }

        /// <summary>
        /// Canonical namespace name.
        /// </summary>
        /// <remarks>In JSON, prior to MediaWiki 1.25, the parameter name was *.</remarks>
        [JsonProperty("canonical")]
        public string Name { get; private set; }

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

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        /// <returns>
        /// 表示当前对象的字符串。
        /// </returns>
        public override string ToString()
        {
            return $"{Name}[{Id}]";
        }
    }
}
