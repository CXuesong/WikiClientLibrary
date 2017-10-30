using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            Page = new WikiPageStub(link.FullTitle, link.Namespace.Id);
        }

        public Board(WikiaSite site, int pageId)
        {
            Site = site ?? throw new ArgumentNullException(nameof(site));
            Page = new WikiPageStub(pageId);
        }

        public Board(WikiPage page)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            Site = (WikiaSite)page.Site;
            // TODO WikiPage should use WikiPageStub as underlying identifier.
            Page = page.ToPageStub();
        }

        public WikiaSite Site { get; }

        public WikiPageStub Page { get; private set; }

        public bool Exists { get; private set; }

        internal void LoadFromPageStub(WikiPageStub stub)
        {
            Page = stub;
            Exists = !stub.IsMissing;
        }

        public Task RefreshAsync()
        {
            return RefreshAsync(CancellationToken.None);
        }

        public Task RefreshAsync(CancellationToken cancellationToken)
        {
            return RequestHelper.RefreshBaordsAsync(new[] { this }, cancellationToken);
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

        /// <inheritdoc cref="NewPostAsync(string,string,IEnumerable{string},PostCreationOptions,CancellationToken)"/>
        public Task<Post> NewPostAsync(string postContent)
        {
            return NewPostAsync(null, postContent);
        }

        /// <inheritdoc cref="NewPostAsync(string,string,IEnumerable{string},PostCreationOptions,CancellationToken)"/>
        public Task<Post> NewPostAsync(string postTitle, string postContent)
        {
            return NewPostAsync(postTitle, postContent, null, PostCreationOptions.None, CancellationToken.None);
        }

        /// <inheritdoc cref="NewPostAsync(string,string,IEnumerable{string},PostCreationOptions,CancellationToken)"/>
        public Task<Post> NewPostAsync(string postTitle, string postContent, IEnumerable<string> relatedPages)
        {
            return NewPostAsync(postTitle, postContent, relatedPages, PostCreationOptions.None, CancellationToken.None);
        }

        /// <inheritdoc cref="NewPostAsync(string,string,IEnumerable{string},PostCreationOptions,CancellationToken)"/>
        public Task<Post> NewPostAsync(string postTitle, string postContent, PostCreationOptions options)
        {
            return NewPostAsync(postTitle, postContent, null, options, CancellationToken.None);
        }

        /// <inheritdoc cref="NewPostAsync(string,string,IEnumerable{string},PostCreationOptions,CancellationToken)"/>
        public Task<Post> NewPostAsync(string postTitle, string postContent, IEnumerable<string> relatedPages,
            PostCreationOptions options)
        {
            return NewPostAsync(postTitle, postContent, relatedPages, options, CancellationToken.None);
        }

        /// <summary>
        /// Asynchronously adds a new reply to the post.
        /// </summary>
        /// <param name="postTitle">Title of the new post, in wikitext format.</param>
        /// <param name="postContent">The content in reply.</param>
        /// <param name="relatedPages">When posting in the Forum, specifies titles of related pages (aka. topics). Can be <c>null</c>.</param>
        /// <param name="options">The options for creating the post.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>A new post containing the ID of the new post.</returns>
        /// <remarks>
        /// <para>Wikia natively supports post title on Message Walls and Forum Boards.
        /// For the unification of method signature, you can set <paramref name="postTitle"/> when posting
        /// article comment, but the content will be simply surrounded with <c>&lt;h2&gt;</c> HTML markup.</para>
        /// <para>If you leave <paramref name="postTitle"/> <c>null</c> or empty when posting on Message Walls,
        /// a default title (e.g. Message from [UserName]) will be used by Wikia.</para>
        /// </remarks>
        public async Task<Post> NewPostAsync(string postTitle, string postContent, IEnumerable<string> relatedPages,
            PostCreationOptions options, CancellationToken cancellationToken)
        {
            // Refresh to get the page id.
            var method = Post.GetPostCreationMethod(Page, options);
            if (method == Post.METHOD_UNKNOWN)
            {
                await RefreshAsync(cancellationToken);
                method = Post.GetPostCreationMethod(Page, options);
                Debug.Assert(method != Post.METHOD_UNKNOWN);
            }
            switch (method)
            {
                case Post.METHOD_ARTICLE_COMMENT:
                    if (!Page.HasId)
                    {
                        await RefreshAsync(cancellationToken);
                        Debug.Assert(Page.IsMissing || Page.HasId);
                    }
                    if (!string.IsNullOrEmpty(postTitle))
                        postContent = "<h2>" + postTitle + "</h2>\n\n" + postContent;
                    return await RequestHelper.PostCommentAsync(Site, this, Page, null, postContent, cancellationToken);
                case Post.METHOD_WALL_MESSAGE:
                    if (!Page.HasTitle)
                    {
                        await RefreshAsync(cancellationToken);
                        Debug.Assert(Page.IsMissing || Page.HasTitle);
                    }
                    return await RequestHelper.PostWallMessageAsync(Site, this, Page, postTitle, postContent,
                        relatedPages, cancellationToken);
            }
            return null;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Page.ToString();
        }

    }
}
