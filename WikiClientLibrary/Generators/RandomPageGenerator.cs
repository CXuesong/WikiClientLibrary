using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using WikiClientLibrary.Client;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Generators.Primitive;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// Gets random pages in a specific namespace.
    /// </summary>
    class RandomPageGenerator : WikiPageGenerator
    {
        /// <inheritdoc />
        public RandomPageGenerator(WikiSite site) : base(site)
        {
        }

        /// <inheritdoc />
        protected override WikiPageStub ItemFromJson(JToken json)
        {
            return new WikiPageStub((int)json["id"], (string)json["title"], (int)json["ns"]);
        }

        /// <summary>
        /// Only list pages in these namespaces.
        /// </summary>
        /// <value>Selected ids of namespace, or <c>null</c> if all the namespaces are selected.</value>
        public IEnumerable<int> NamespaceIds { get; set; }

        /// <summary>
        /// How to filter redirects.
        /// </summary>
        public PropertyFilterOption RedirectsFilter { get; set; }

        /// <inheritdoc/>
        public override string ListName => "random";

        /// <inheritdoc/>
        public override IEnumerable<KeyValuePair<string, object>> EnumListParameters()
        {
            return new Dictionary<string, object>
            {
                {"rnnamespace", NamespaceIds == null ? null : MediaWikiHelper.JoinValues(NamespaceIds)},
                {"rnfilterredir", RedirectsFilter.ToString("redirects", "nonredirects")},
                {"rnlimit", PaginationSize},
            };
        }
    }
}
