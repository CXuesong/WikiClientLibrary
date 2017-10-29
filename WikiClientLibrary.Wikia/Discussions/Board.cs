using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Pages;

namespace WikiClientLibrary.Wikia.Discussions
{
    public class Board
    {
        public Board(WikiaSite site, string title) : this(site, title, BuiltInNamespaces.Main)
        {
        }

        public Board(WikiaSite site, string title, int defaultNamespaceId)
        {
            Site = site ?? throw new ArgumentNullException(nameof(site));
            var link = WikiLink.Parse(site, title, defaultNamespaceId);
            NamespaceId = link.Namespace.Id;
            Title = link.FullTitle;
        }

        public Board(WikiaSite site, int pageId)
        {
            Site = site ?? throw new ArgumentNullException(nameof(site));
            Id = pageId;
            NamespaceId = -100;
            Title = null;
        }

        public Board(WikiPage page)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            Site = (WikiaSite)page.Site;
            NamespaceId = page.NamespaceId;
            Title = page.Title;
            Id = page.Id;
        }

        public WikiaSite Site { get; }

        public int Id { get; private set; }

        public int NamespaceId { get; private set; }

        public string Title { get; private set; }

        public bool Exists { get; private set; }

        public Task RefreshAsync()
        {
            return RefreshAsync(CancellationToken.None);
        }

        public async Task RefreshAsync(CancellationToken cancellationToken)
        {
            var jresult = await Site.GetJsonAsync(new MediaWikiFormRequestMessage(new
            {
                action = "query",
                titles = Title
            }), cancellationToken);
            var jpage = ((JProperty)jresult["query"]["pages"].First).Value;
            Exists = jpage["missing"] == null;
            Id = Exists ? (int)jpage["pageid"] : -1;
            NamespaceId = (int)jpage["ns"];
            Title = (string)jpage["title"];
        }

        /// <inheritdoc cref="EnumPostsAsync(PostQueryOptions)"/>
        public IAsyncEnumerable<Post> EnumPostsAsync()
        {
            return EnumPostsAsync(PostQueryOptions.None);
        }

        /// <summary>
        /// Asynchronously enumerates all the comments on the specified page.
        /// </summary>
        /// <param name="options">The options used to fetch the post.</param>
        public IAsyncEnumerable<Post> EnumPostsAsync(PostQueryOptions options)
        {
            return RequestHelper.EnumArticleCommentsAsync(this, options);
        }

        /// <inheritdoc cref="NewPostAsync(string,CancellationToken)"/>
        public Task<Post> NewPostAsync(string content)
        {
            return NewPostAsync(content, CancellationToken.None);
        }

        /// <summary>
        /// Asynchronously adds a new reply to the post.
        /// </summary>
        /// <param name="content">The content in reply.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <exception cref="InvalidOperationException">The specified page does not exist.</exception>
        /// <returns>A new post containing the workflow ID of the new post.</returns>
        public async Task<Post> NewPostAsync(string content, CancellationToken cancellationToken)
        {
            // Refresh to get the page id.
            if (Id == 0) await RefreshAsync(cancellationToken);
            if (!Exists) throw new InvalidOperationException("The specified page does not exist.");
            return await RequestHelper.PostCommentAsync(Site, this, Id, null, content, cancellationToken);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.IsNullOrEmpty(Title) ? ("#" + Id) : Title;
        }

    }
}
