using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
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
    public class Board : IWikiClientLoggable
    {

        private ILoggerFactory _LoggerFactory;
        private ILogger logger = NullLogger.Instance;
        private string editToken;

        /// <summary>
        /// Initializes a new <see cref="Board"/> instance from MW site and board page title.
        /// </summary>
        /// <param name="site">MediaWiki site.</param>
        /// <param name="title">Full page title of the Flow discussion board, including namespace prefix.</param>
        /// <exception cref="ArgumentNullException">Either <paramref name="site"/> or <paramref name="title"/> is <c>null</c>.</exception>
        public Board(WikiSite site, string title)
        {
            Site = site ?? throw new ArgumentNullException(nameof(site));
            Title = title ?? throw new ArgumentNullException(nameof(title));
            LoggerFactory = site.LoggerFactory;
        }

        /// <summary>The MediaWiki site hosting this board.</summary>
        public WikiSite Site { get; }

        /// <summary>Full title of the board page.</summary>
        public string Title { get; }

        /// <summary>Latest header content revision.</summary>
        public Revision HeaderRevision { get; private set; }

        /// <inheritdoc cref="RefreshAsync(CancellationToken)"/>
        public Task RefreshAsync()
        {
            return RefreshAsync(CancellationToken.None);
        }

        /// <summary>
        /// Fetches board header information from server.
        /// </summary>
        public async Task RefreshAsync(CancellationToken cancellationToken)
        {
            // Known Issue: view-header doesn't support multiple page names.
            var jresult = await Site.GetJsonAsync(new WikiFormRequestMessage(new
            {
                action = "flow",
                submodule = "view-header",
                page = Title,
                vhformat = "wikitext"
            }), cancellationToken);
            var jheader = jresult["flow"]["view-header"]["result"]["header"];
            editToken = (string)jheader["editToken"];
            HeaderRevision = jheader["revision"]?.ToObject<Revision>(FlowUtility.FlowJsonSerializer);
            // (string)jheader["copyrightMessage"]
        }

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
                var jresult = await Site.GetJsonAsync(new WikiFormRequestMessage(queryParams), ct);
                var jtopiclist = (JObject)jresult["flow"]["view-topiclist"]["result"]["topiclist"];
                // ISSUE directly return SelectArrayIterator<TSource, TResult> via Enumerable.Select
                // can cause strange behavior when chained with other async LINQ methods. See
                // https://github.com/CXuesong/WikiClientLibrary/issues/27
                var topics = Topic.FromJsonTopicList(Site, jtopiclist).ToList();
                // TODO Implement Pagination
                eof = true;
                return Tuple.Create((IEnumerable<Topic>)topics, true);
            });
            return ienu.SelectMany(t => t.ToAsyncEnumerable());
        }

        /// <inheritdoc cref="NewTopicAsync(string,string,CancellationToken)"/>
        public Task<Topic> NewTopicAsync(string topicTitle, string topicContent)
        {
            return NewTopicAsync(topicTitle, topicContent, CancellationToken.None);
        }

        /// <summary>
        /// Creates a new topic on the current board.
        /// </summary>
        /// <param name="topicTitle">Title of the new topic, in wikitext format.</param>
        /// <param name="topicContent">First post content of the new topic, in wikitext format.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>A task that returns the newly-created topic when succeeds.</returns>
        public Task<Topic> NewTopicAsync(string topicTitle, string topicContent, CancellationToken cancellationToken)
        {
            if (topicTitle == null) throw new ArgumentNullException(nameof(topicTitle));
            if (topicContent == null) throw new ArgumentNullException(nameof(topicContent));
            return FlowRequestHelper.NewTopicAsync(Site, Title, topicTitle, topicContent, cancellationToken);
        }

        /// <inheritdoc />
        public ILoggerFactory LoggerFactory
        {
            get => _LoggerFactory;
            set => logger = Utility.SetLoggerFactory(ref _LoggerFactory, value, GetType());
        }

        /// <inheritdoc />
        public override string ToString() => Title;
    }
}
