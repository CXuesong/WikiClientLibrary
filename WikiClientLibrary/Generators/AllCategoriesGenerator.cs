using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WikiClientLibrary.Generators.Primitive;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// Generates all the categories with or without description pages.
    /// </summary>
    public class AllCategoriesGenerator : WikiPageGenerator
    {

        /// <inheritdoc />
        public AllCategoriesGenerator(WikiSite site) : base(site)
        {
        }

        /// <summary>
        /// Start listing at this title. The title does not have to exist.
        /// </summary>
        public string StartTitle { get; set; } = "!";

        /// <summary>
        /// The page title to stop enumerating at.
        /// </summary>
        public string? EndTitle { get; set; } = null;

        /// <summary>
        /// Search for all category titles that begin with this value.
        /// </summary>
        public string? Prefix { get; set; }

        /// <summary>
        /// Minimum number of category members.
        /// </summary>
        public int? MinChildrenCount { get; set; }

        /// <summary>
        /// Maximum number of category members.
        /// </summary>
        public int? MaxChildrenCount { get; set; }

        /// <inheritdoc />
        public override string ListName => "allcategories";

        /// <inheritdoc/>
        public override IEnumerable<KeyValuePair<string, object>> EnumListParameters()
        {
            return new Dictionary<string, object>
            {
                {"acfrom", StartTitle},
                {"acto", EndTitle},
                {"aclimit", PaginationSize},
                {"acprefix", Prefix},
                {"cmin", MinChildrenCount},
                {"cmax", MaxChildrenCount},
            };
        }
    }
}
