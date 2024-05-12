namespace WikiClientLibrary.Wikia.Discussions;

public static class DiscussionsExtensions
{

    /// <inheritdoc cref="RefreshAsync(IEnumerable{Post},PostQueryOptions,CancellationToken)"/>
    /// <seealso cref="Post.RefreshAsync()"/>
    public static Task RefreshAsync(this IEnumerable<Post> posts)
    {
        return RefreshAsync(posts, PostQueryOptions.None, new CancellationToken());
    }

    /// <inheritdoc cref="RefreshAsync(IEnumerable{Post},PostQueryOptions,CancellationToken)"/>
    /// <seealso cref="Post.RefreshAsync(PostQueryOptions)"/>
    public static Task RefreshAsync(this IEnumerable<Post> posts, PostQueryOptions options)
    {
        return RefreshAsync(posts, options, new CancellationToken());
    }

    /// <summary>
    /// Refreshes the post content from the server.
    /// </summary>
    /// <param name="posts">The posts to be refreshed.</param>
    /// <param name="options">The options used to fetch the post.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <remarks>
    /// This method will not fetch replies for the <paramref name="posts"/>. <see cref="Post.Replies"/> will remain unchanged after the invocation.
    /// </remarks>
    /// <seealso cref="Post.RefreshAsync(PostQueryOptions,CancellationToken)"/>
    public static Task RefreshAsync(this IEnumerable<Post> posts, PostQueryOptions options, CancellationToken cancellationToken)
    {
        if (posts == null) throw new ArgumentNullException(nameof(posts));
        return RequestHelper.RefreshPostsAsync(posts, options, cancellationToken);
    }

}
