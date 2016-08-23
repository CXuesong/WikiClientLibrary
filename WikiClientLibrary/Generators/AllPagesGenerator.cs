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
        public int NamespaceId { get; }
        public string StartTitle { get; }

        protected override Task SubscribeAsync(IObserver<Page> observer, CancellationToken cancellationToken)
        {
            //var jresult = Client.GetJsonAsync(new
            //{
            //    action = "query",
            //    list = "allpages",
            //    apfrom = ""
            //})
            throw new NotImplementedException();
        }

        /// <summary>
        /// When overridden, fills generator parameters for action=query request.
        /// </summary>
        /// <param name="queryDictionary">The dictioanry containning request value pairs.</param>
        protected override void FillQueryRequestParams(IDictionary<string, string> queryDictionary)
        {
            throw new NotImplementedException();
        }

        public AllPagesGenerator(Site site, int namespaceId) : this(site, namespaceId, "!")
        {
        }

        public AllPagesGenerator(Site site, int namespaceId, string startTitle) : base(site)
        {
            NamespaceId = namespaceId;
            StartTitle = startTitle;
        }
    }
}