using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Generators.Primitive;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// List all pages using a given page property. The list of available properties can be found at action=query&amp;list=pagepropnames.
    /// </summary>
    public class PagesWithPropGenerator : WikiPageGenerator<PagesWithPropResultItem>
    {
        public PagesWithPropGenerator(WikiSite site, string propertyName) : base(site)
        {
            PropertyName = propertyName;
        }

        /// <summary>
        /// Page property for which to enumerate pages (action=query&list=pagepropnames returns page property names in use).
        /// </summary>
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>
        /// Gets/sets a value that indicates whether the links should be listed in
        /// the descending order. (MediaWiki 1.19+)
        /// </summary>
        public bool OrderDescending { get; set; }
    
        public override string ListName => "pageswithprop";

        public override IEnumerable<KeyValuePair<string, object?>> EnumListParameters()
        {
            return new Dictionary<string, object?>
            {
                {"pwpprop", "ids|title|value"},
                {"pwppropname", PropertyName},
                {"pwplimit", PaginationSize},
                {"pwpdir", OrderDescending ? "descending" : "ascending"},
            };
        }

        protected override PagesWithPropResultItem ItemFromJson(JToken json)
        {
            return new PagesWithPropResultItem(MediaWikiHelper.PageStubFromJson((JObject)json), (string)json["value"]);
        }
    }
}