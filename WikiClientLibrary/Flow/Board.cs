using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
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
            Header = new BoardHeader(site, title);
            LoggerFactory = site.LoggerFactory;
        }

        /// <summary>
        /// The MediaWiki site hosting this board.
        /// </summary>
        public WikiSite Site { get; }

        /// <summary>
        /// Full title of the board.
        /// </summary>
        public string Title { get; }

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
                var jresult = await Site.GetJsonAsync(new WikiFormRequestMessage(queryParams), ct);
                var jtopiclist = (JObject) jresult["flow"]["view-topiclist"]["result"]["topiclist"];
                var topics = Topic.FromJsonTopicListResult(Site, jtopiclist);
                // TODO Implement Pagination
                eof = true;
                return Tuple.Create((IEnumerable<Topic>) topics, true);
            });
            return ienu.SelectMany(t => t.ToAsyncEnumerable());
        }

        /// <inheritdoc />
        public ILoggerFactory LoggerFactory
        {
            get => _LoggerFactory;
            set => logger = Utility.SetLoggerFactory(ref _LoggerFactory, value, GetType());
        }

    }
}
