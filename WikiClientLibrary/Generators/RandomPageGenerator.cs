using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using WikiClientLibrary.Client;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Generators.Primitive;
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

        /// <inheritdoc />
        public int NamespaceId { get; set; } = 0;

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
                { "rnnamespace", NamespaceId},
                {"rnfilterredir", RedirectsFilter},
                {"rnlimit", PaginationSize},
            };
        }
    }
}
