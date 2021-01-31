using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Pages.Queries.Properties
{
    public class PageInfoPropertyProvider : WikiPagePropertyProvider<PageInfoPropertyGroup>
    {
        private static readonly IEnumerable<KeyValuePair<string, object>> fixedProp = new ReadOnlyCollection<KeyValuePair<string, object>>(
            new OrderedKeyValuePairs<string, object>
            {
                {"inprop", "protection"}
            });        /// <inheritdoc />
        public override IEnumerable<KeyValuePair<string, object>> EnumParameters(MediaWikiVersion version)
        {
            return fixedProp;
        }

        /// <inheritdoc />
        public override PageInfoPropertyGroup ParsePropertyGroup(JObject json)
        {
            return new PageInfoPropertyGroup(json);
        }

        /// <inheritdoc />
        public override string PropertyName => "info";
    }

    public class PageInfoPropertyGroup : WikiPagePropertyGroup
    {

        protected internal PageInfoPropertyGroup(JObject jPage)
        {
            ContentModel = (string)jPage["contentmodel"];
            PageLanguage = (string)jPage["pagelanguage"];
            PageLanguageDirection = (string)jPage["pagelanguagedir"];
            IsRedirect = jPage["redirect"] != null;
            if (jPage["missing"] != null || jPage["invalid"] != null || jPage["special"] != null)
            {
                ContentLength = 0;
                LastRevisionId = 0;
                Protections = null;
                LastTouched = DateTime.MinValue;
                Protections = null;
                RestrictionTypes = null;
            }
            else
            {
                ContentLength = (int)jPage["length"];
                LastRevisionId = (int)jPage["lastrevid"];
                LastTouched = (DateTime)jPage["touched"];
                if (jPage["protection"] != null)
                    Protections = jPage["protection"].HasValues
                        ? jPage["protection"].ToObject<IReadOnlyCollection<ProtectionInfo>>(Utility.WikiJsonSerializer)
                        : Array.Empty<ProtectionInfo>();
                if (jPage["restrictiontypes"] != null)
                    RestrictionTypes = jPage["restrictiontypes"].HasValues
                        ? jPage["restrictiontypes"].ToObject<IReadOnlyCollection<string>>(Utility.WikiJsonSerializer)
                        : Array.Empty<string>();
            }
        }

        public string ContentModel { get; }

        public string PageLanguage { get; }

        public string PageLanguageDirection { get; }

        public DateTime LastTouched { get; }

        public int LastRevisionId { get; }

        public int ContentLength { get; }

        public bool IsRedirect { get; }

        public IReadOnlyCollection<ProtectionInfo> Protections { get; }

        /// <summary>
        /// Applicable protection types. (MediaWiki 1.25)
        /// </summary>
        public IReadOnlyCollection<string> RestrictionTypes { get; }

    }
}
