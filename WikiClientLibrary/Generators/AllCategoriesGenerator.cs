using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Generators
{
    /// <summary>
    /// Generates all the categories with or without description pages.
    /// </summary>
    public class AllCategoriesGenerator : PageGenerator<Category>
    {
        public AllCategoriesGenerator(Site site) : base(site)
        {
        }

        /// <summary>
        /// Start listing at this title. The title need not exist.
        /// </summary>
        public string StartTitle { get; set; } = "!";

        /// <summary>
        /// The page title to stop enumerating at.
        /// </summary>
        public string EndTitle { get; set; } = null;

        /// <summary>
        /// Search for all category titles that begin with this value.
        /// </summary>
        public string Prefix { get; set; }

        /// <summary>
        /// Minimum number of category members.
        /// </summary>
        public int? MinChildrenCount { get; set; }

        /// <summary>
        /// Maximum number of category members.
        /// </summary>
        public int? MaxChildrenCount { get; set; }

        /// <summary>
        /// When overridden, fills generator parameters for action=query request.
        /// </summary>
        /// <param name="actualPagingSize"></param>
        /// <returns>The dictioanry containing request value pairs.</returns>
        protected override IEnumerable<KeyValuePair<string, object>> GetGeneratorParams(int actualPagingSize)
        {
            return new Dictionary<string, object>
            {
                {"generator", "allcategories"},
                {"gacfrom", StartTitle},
                {"gacto", EndTitle},
                {"gaclimit", actualPagingSize},
                {"gacprefix", Prefix},
                {"acmin", MinChildrenCount},
                {"acmax", MaxChildrenCount},
            };
        }
    }
}
