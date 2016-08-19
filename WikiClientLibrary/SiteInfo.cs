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
        [JsonProperty]      // This should be kept or private setter will be ignored by Newtonsoft.JSON.
        public string MainPage { get; private set; }

        /// <summary>
        /// The absolute path to the main page.
        /// </summary>
        [JsonProperty("base")]
        public string BaseUrl { get; private set; }

        [JsonProperty]
        public string SiteName { get; private set; }

        [JsonProperty("logo")]
        public string LogoUrl { get; private set; }

        [JsonProperty]
        public string Generator { get; private set; }

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
