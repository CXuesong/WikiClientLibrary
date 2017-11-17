using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Pages.Queries.Properties
{
    public class PagePropertyPropertyProvider : WikiPagePropertyProvider<PagePropertyPropertyGroup>
    {
        /// <inheritdoc />
        public override IEnumerable<KeyValuePair<string, object>> EnumParameters()
        {
            return new OrderedKeyValuePairs<string, object>
            {
                {"ppprop", SelectedProperties == null ? null : string.Join("|", SelectedProperties)}
            };
        }

        /// <inheritdoc />
        public override PagePropertyPropertyGroup ParsePropertyGroup(JObject json)
        {
            return PagePropertyPropertyGroup.Create(json);
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

    public class PagePropertyPropertyGroup : WikiPagePropertyGroup
    {

        private static readonly PagePropertyPropertyGroup Empty = new PagePropertyPropertyGroup();

        internal static PagePropertyPropertyGroup Create(JObject jpage)
        {
            var props = jpage["pageprops"];
            if (props == null) return null;
            if (!props.HasValues) return Empty;
            return new PagePropertyPropertyGroup(jpage);
        }

        private PagePropertyPropertyGroup()
        {
            PageProperties = PagePropertyCollection.Empty;
        }

        private PagePropertyPropertyGroup(JObject jPage)
        {
            PageProperties = jPage["pageprops"]?.ToObject<PagePropertyCollection>(Utility.WikiJsonSerializer);
        }

        /// <summary>
        /// Gets the properties of the page.
        /// </summary>
        public PagePropertyCollection PageProperties { get; }

    }
}
