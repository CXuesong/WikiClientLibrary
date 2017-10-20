using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// Represents an item in the search result.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class SearchResultItem
    {

        /// <summary>
        /// Id of the page.
        /// </summary>
        [JsonProperty("pageid")]
        public string Id { get; private set; }

        /// <summary>
        /// Namespace id of the page.
        /// </summary>
        [JsonProperty("ns")]
        public string NamespaceId { get; private set; }

        /// <summary>
        /// Gets the full title of the page.
        /// </summary>
        [JsonProperty]
        public string Title { get; private set; }

        /// <summary>
        /// Gets the content length, in bytes.
        /// </summary>
        [JsonProperty("size")]
        public int ContentLength { get; private set; }

        /// <summary>
        /// Gets the word count.
        /// </summary>  
        [JsonProperty]
        public int WordCount { get; private set; }

        /// <summary>
        /// Gets the parsed HTML snippet of the page.
        /// </summary>  
        [JsonProperty]
        public string Snippet { get; private set; }

        /// <summary>
        /// Gets the timestamp of when the page was last edited.
        /// </summary>  
        [JsonProperty]
        public DateTime TimeStamp { get; private set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"[{Id}]{Title}";
        }
    }
}
