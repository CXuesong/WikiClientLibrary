using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WikiClientLibrary
{
    /// <summary>
    /// Contains parsed content of specific page or wikitext.
    /// </summary>
    /// <remarks>Use <see cref="Site.ParsePage"/> to get parsed content.</remarks>
    [JsonObject(MemberSerialization.OptIn)]
    public class ParsedContentInfo
    {
        [JsonProperty]
        public string Title { get; private set; }

        [JsonProperty]
        public string DisplayTitle { get; private set; }

        [JsonProperty]
        public int PageId { get; private set; }

        [JsonProperty("revid")]
        public int RevisionId { get; private set; }

        /// <summary>
        /// Parsed text, in HTML form.
        /// </summary>
        public string Text { get; private set; }

        [JsonProperty("text")]
        private JToken DummyText {
            get { return new JObject(new JProperty("*", Text)); }
            set { Text = (string)value["*"]; }
        }

        [JsonProperty]
        public IReadOnlyCollection<InterlanguageInfo> Interlanguages { get; private set; }

        [JsonProperty]
        public IReadOnlyCollection<ContentCategoryInfo> Categories { get; private set; }

        [JsonProperty]
        public IReadOnlyCollection<ContentSectionInfo> Sections { get; private set; }

        [JsonProperty]
        public IReadOnlyCollection<ContentPropertyInfo> Properties { get; private set; }

        /// <summary>
        /// Determins the redirects that has been followed to reach the page.
        /// </summary>
        [JsonProperty]
        public IReadOnlyCollection<ContentRedirectInfo> Redirects { get; private set; }

        /// <summary>
        /// Determins whether one or more redirects has been followed to reach the page.
        /// </summary>
        public bool IsRedirected { get; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class ContentRedirectInfo
    {
        [JsonProperty]
        public string From { get; private set; }

        [JsonProperty]
        public string To { get; private set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class ContentPropertyInfo
    {
        [JsonProperty]
        public string Name { get; private set; }

        [JsonProperty("*")]
        public string Value { get; private set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class ContentSectionInfo
    {
        [JsonProperty]
        public int Index { get; private set; }

        /// <summary>
        /// Heading text.
        /// </summary>
        [JsonProperty("line")]
        public string Heading { get; private set; }

        /// <summary>
        /// Anchor name of the heading.
        /// </summary>
        [JsonProperty]
        public string Anchor { get; private set; }

        /// <summary>
        /// Heading number. E.g. 3.2 .
        /// </summary>
        [JsonProperty]
        public string Number { get; private set; }

        /// <summary>
        /// Level of the heading.
        /// </summary>
        [JsonProperty]
        public int Level { get; private set; }

        /// <summary>
        /// Toc level of the heading. This is usually <see cref="Level"/> - 1.
        /// </summary>
        [JsonProperty]
        public int TocLevel { get; private set; }

        /// <summary>
        /// Title of the page.
        /// </summary>
        [JsonProperty("fromtitle")]
        public string PageTitle { get; private set; }

        public int ByteOffset { get; private set; }

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        /// <returns>
        /// 表示当前对象的字符串。
        /// </returns>
        public override string ToString()
        {
            return PageTitle + "#" + Heading;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class ContentCategoryInfo
    {
        /// <summary>
        /// Title of the category.
        /// </summary>
        [JsonProperty("*")]
        public string CategoryName { get; private set; }

        [JsonProperty]
        public string SortKey { get; private set; }

        [JsonProperty]
        public bool IsHidden { get; private set; }

    }

    [JsonObject(MemberSerialization.OptIn)]
    public class InterlanguageInfo
    {
        [JsonProperty("lang")]
        public string Language { get; private set; }

        [JsonProperty]
        public string Url { get; private set; }

        /// <summary>
        /// Autonym of the languge.
        /// </summary>
        [JsonProperty]
        public string Autonym { get; private set; }

        /// <summary>
        /// Title of the page in the specified language.
        /// </summary>
        [JsonProperty("*")]
        public string PageTitle { get; private set; }

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        /// <returns>
        /// 表示当前对象的字符串。
        /// </returns>
        public override string ToString()
        {
            return $"{Language}:{PageTitle}";
        }
    }
}
