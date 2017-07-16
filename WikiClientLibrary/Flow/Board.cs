using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Flow
{
    /// <summary>
    /// Represents a Flow topic page.
    /// </summary>
    /// <remarks>
    /// <para>The content model of such page should be <c>flow-board</c>.</para>
    /// <para>See https://www.mediawiki.org/wiki/Extension:Flow for more information about Flow extension.</para>
    /// <para>Note that the development of Flow extension seems now paused. See https://en.wikipedia.org/wiki/Wikipedia:Flow for more information.</para>
    /// </remarks>
    public class Board : WikiPage
    {
        /// <inheritdoc />
        public Board(WikiSite site, string title) : this(site, title, BuiltInNamespaces.Main)
        {
        }

        /// <inheritdoc />
        public Board(WikiSite site, string title, int defaultNamespaceId) : base(site, title, defaultNamespaceId)
        {
            Header = new BoardHeader(site, title);
        }

        /// <inheritdoc />
        internal Board(WikiSite site) : base(site)
        {

        }

        public BoardHeader Header { get; private set; }

        /// <summary>
        /// Asynchronously enumerates the topics in this board.
        /// </summary>
        public IAsyncEnumerable<Topic> EnumTopicsAsync()
        {
            return EnumTopicsAsync(20);
        }

        /// <summary>
        /// Asynchronously enumerates the topics in this board.
        /// </summary>
        public IAsyncEnumerable<Topic> EnumTopicsAsync(int pageSize)
        {
            if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));
            var queryParams = new Dictionary<string, object>
            {
                {"action", "flow"},
                {"submodule", "view-topiclist"},
                {"page", Title},
                {"vtllimit", pageSize},
                {"vtlformat", "wikitext"},
            };
            var eof = false;
            var ienu = new DelegateAsyncEnumerable<IEnumerable<Topic>>(async ct =>
            {
                if (eof) return null;
                var jresult = await Site.PostValuesAsync(queryParams, ct);
                var jtopiclist = (JObject) jresult["flow"]["view-topiclist"]["result"]["topiclist"];
                var topics = Topic.FromJsonTopicListResult(Site, jtopiclist);
                // TODO Implement Pagination
                eof = true;
                return Tuple.Create((IEnumerable<Topic>) topics, true);
            });
            return ienu.SelectMany(t => t.ToAsyncEnumerable());
        }

        /// <inheritdoc />
        protected override void OnLoadPageInfo(JObject jpage)
        {
            base.OnLoadPageInfo(jpage);
            if (Header == null) Header = new BoardHeader(Site, Title);
        }

        /// <summary>
        /// Infrastructure.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public new string Content
        {
            get { return base.Content; }
            set { base.Content = value; }
        }
    }
}
