using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AsyncEnumerableExtensions;
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
    public class Board
    {

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
        }

        /// <summary>The MediaWiki site hosting this board.</summary>
        public WikiSite Site { get; }

        /// <summary>Full title of the board page.</summary>
        public string Title { get; }

        /// <summary>Latest header content revision.</summary>
        /// <value>The latest header revision, or <c>null</c> if the board header does not exist.</value>
        public Revision HeaderRevision { get; private set; }

        /// <summary>Header content.</summary>
        public string HeaderContent { get; set; }

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
            var jresult = await Site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new
            {
                action = "flow",
                submodule = "view-header",
                page = Title,
                vhformat = "wikitext"
            }), cancellationToken);
            var jheader = jresult["flow"]["view-header"]["result"]["header"];
            editToken = (string)jheader["editToken"];
            var rev = jheader["revision"]?.ToObject<Revision>(FlowUtility.FlowJsonSerializer);
            HeaderRevision = rev;
            // (string)jheader["copyrightMessage"]
            HeaderContent = HeaderRevision?.Content;
        }

        /// <summary>
        /// Asynchronously enumerates the topics in the order of time posted in this board.
        /// </summary>
        public IAsyncEnumerable<Topic> EnumTopicsAsync()
        {
            return EnumTopicsAsync(TopicListingOptions.OrderByPosted, 20);
        }

        /// <inheritdoc cref="EnumTopicsAsync(TopicListingOptions, int)"/>
        public IAsyncEnumerable<Topic> EnumTopicsAsync(TopicListingOptions options)
        {
            return EnumTopicsAsync(options, 20);
        }

        /// <summary>
        /// Asynchronously enumerates the topics in this board.
        /// </summary>
        /// <param name="options">Enumeration options.</param>
        /// <param name="pageSize">
        /// How many topics should be fetched in batch per MediaWiki API request.
        /// No more than 100 (100 for bots) is allowed.
        /// </param>
        public IAsyncEnumerable<Topic> EnumTopicsAsync(TopicListingOptions options, int pageSize)
        {
            if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));
            var sortParam = "user";
            if ((options & TopicListingOptions.OrderByPosted) == TopicListingOptions.OrderByPosted)
                sortParam = "newest";
            if ((options & TopicListingOptions.OrderByUpdated) == TopicListingOptions.OrderByUpdated)
                sortParam = "updated";
            return AsyncEnumerableFactory.FromAsyncGenerator<Topic>(async (sink, ct) =>
            {
                var queryParams = new Dictionary<string, object>
                {
                    {"action", "flow"},
                    {"submodule", "view-topiclist"},
                    {"page", Title},
                    {"vtlsortby", sortParam},
                    {"vtlsavesortby", (options & TopicListingOptions.SaveSortingPreference) == TopicListingOptions.SaveSortingPreference},
                    {"vtllimit", pageSize},
                    {"vtlformat", "wikitext"},
                };
                NEXT_PAGE:
                var jresult = await Site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(queryParams), ct);
                var jtopiclist = (JObject)jresult["flow"]["view-topiclist"]["result"]["topiclist"];
                await sink.YieldAndWait(Topic.FromJsonTopicList(Site, jtopiclist));
                // 2018-07-30 flow.view-topiclist.result.topiclist.links.pagination is [] instead of null for boards without pagination.
                var jpagination = jtopiclist["links"]?["pagination"];
                var nextPageUrl = jpagination == null || jpagination is JArray 
                    ? null 
                    : (string) jpagination["fwd"]?["url"];
                if (nextPageUrl != null)
                {
                    var urlParams = FlowUtility.ParseUrlQueryParametrs(nextPageUrl);
                    foreach (var pa in urlParams)
                    {
                        if (pa.Key.StartsWith("topiclist_"))
                            queryParams["vtl" + pa.Key[10..]] = pa.Value;
                    }
                    goto NEXT_PAGE;
                }
            });
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
        /// <returns>
        /// A task that returns the newly-created topic when succeeds.
        /// The instance only contains the workflow ID, so you may need to call
        /// <see cref="Topic.RefreshAsync()"/> if you want to query more information about it. 
        /// </returns>
        public Task<Topic> NewTopicAsync(string topicTitle, string topicContent, CancellationToken cancellationToken)
        {
            if (topicTitle == null) throw new ArgumentNullException(nameof(topicTitle));
            if (topicContent == null) throw new ArgumentNullException(nameof(topicContent));
            return FlowRequestHelper.NewTopicAsync(Site, Title, topicTitle, topicContent, cancellationToken);
        }

        /// <inheritdoc />
        public override string ToString() => Title;
    }

    /// <summary>
    /// Defines how a Flow topic list should be enumerated.
    /// </summary>
    [Flags]
    public enum TopicListingOptions
    {
        /// <summary>Use user's default sorting preference.</summary>
        Default = 0,
        /// <summary>Flow topic list should be sorted descending by the time a topic is posed.</summary>
        OrderByPosted = 1,
        /// <summary>Flow topic list should be sorted descending by the time a topic's last activity.</summary>
        OrderByUpdated = 2,
        /// <summary>Save the current sorting option as user's preference.</summary>
        SaveSortingPreference = 4,
    }

}
