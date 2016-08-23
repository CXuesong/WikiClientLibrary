using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WikiClientLibrary.Generators
{
    public class AllPagesGenerator : PageGenerator
    {
        public int NamespaceId { get; set; } = 0;

        public string StartTitle { get; set; } = "!";

        /// <summary>
        /// Maximum items returned per request.
        /// </summary>
        /// <value>
        /// Maximum count of items returned per request.
        /// <c>null</c> if using the default limit.
        /// (5000 for bots and 500 for users.)
        /// </value>
        public int? PagingSize { get; set; }

        /// <summary>
        /// When overridden, fills generator parameters for action=query request.
        /// </summary>
        /// <returns>The dictioanry containning request value pairs.</returns>
        protected override IEnumerable<KeyValuePair<string, string>> GetGeneratorParams()
        {
            return new Dictionary<string, string>
            {
                {"generator", "allpages"},
                {"gapfrom", StartTitle},
                {
                    "gaplimit", Convert.ToString(PagingSize ?? (
                        Site.UserInfo.HasRight(UserRights.ApiHighLimits) ? 5000 : 500))
                },
                {"gapnamespace", Convert.ToString(NamespaceId)},
            };
        }

        public AllPagesGenerator(Site site) : base(site)
        {
        }
    }
}