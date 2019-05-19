using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Pages.Queries.Properties
{
    public class PagePropertiesPropertyProvider : WikiPagePropertyProvider<PagePropertiesPropertyGroup>
    {        /// <inheritdoc />
        public override IEnumerable<KeyValuePair<string, object>> EnumParameters(MediaWikiVersion version)
        {
            return new OrderedKeyValuePairs<string, object>
            {
                {"ppprop", SelectedProperties == null ? null : MediaWikiHelper.JoinValues(SelectedProperties)}
            };
        }

        /// <inheritdoc />
        public override PagePropertiesPropertyGroup ParsePropertyGroup(JObject json)
        {
            return PagePropertiesPropertyGroup.Create(json);
        }

        /// <summary>
        /// Only list these page properties (<c>action=query&amp;list=pagepropnames</c> returns page property names in use).
        /// Useful for checking whether pages use a certain page property.
        /// </summary>
        /// <value>A sequence of selected property names, or <c>null</c> to select all of the properties.</value>
        public IEnumerable<string> SelectedProperties { get; set; }

        /// <inheritdoc />
        public override string PropertyName => "pageprops";
    }

    public class PagePropertiesPropertyGroup : WikiPagePropertyGroup
    {

        private static readonly PagePropertiesPropertyGroup Empty = new PagePropertiesPropertyGroup();

        internal static PagePropertiesPropertyGroup Create(JObject jpage)
        {
            var props = jpage["pageprops"];
            // jpage["pageprops"] == null for pages with no pageprop item,
            // even if client specified prop=pageprops
            if (props == null) return Empty;
            if (!props.HasValues) return Empty;
            return new PagePropertiesPropertyGroup(jpage);
        }

        private PagePropertiesPropertyGroup()
        {
            PageProperties = PagePropertyCollection.Empty;
        }

        private PagePropertiesPropertyGroup(JObject jPage)
        {
            PageProperties = jPage["pageprops"]?.ToObject<PagePropertyCollection>(Utility.WikiJsonSerializer);
        }

        /// <summary>
        /// Gets the properties of the page.
        /// </summary>
        public PagePropertyCollection PageProperties { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            return PageProperties.ToString();
        }
    }
}
