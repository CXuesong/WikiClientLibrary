using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Wikia.WikiaApi
{
    public class RelatedPageItem
    {

        /// <summary>Absolute URL of the page.</summary>
        [JsonProperty("url")]
        public string Url { get; private set; }

        /// <summary>Full title of the page.</summary>
        [JsonProperty("title")]
        public string Title { get; private set; }

        /// <summary>ID of the page.</summary>
        [JsonProperty("id")]
        public int Id { get; private set; }

        [JsonProperty("imgUrl")]
        public string ImageUrl { get; private set; }

        [JsonProperty("imgOriginalDimensions")]
        public string ImageOriginalDimensions { get; private set; }

        /// <summary>Excerpt of the page.</summary>
        [JsonProperty("text")]
        public string Text { get; private set; }

        internal void ApplyBasePath(string basePath)
        {
            if (Url != null) Url = MediaWikiHelper.MakeAbsoluteUrl(basePath, Url);
            if (ImageUrl != null) ImageUrl = MediaWikiHelper.MakeAbsoluteUrl(basePath, ImageUrl);
        }

        /// <inheritdoc />
        public override string ToString() => $"[{Id}]{Title}";
    }
}
