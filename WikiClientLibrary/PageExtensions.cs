using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WikiClientLibrary.Generators;

namespace WikiClientLibrary
{
    /// <summary>
    /// Provides extension methods for a sequence of <see cref="Page"/>.
    /// </summary>
    public static class PageExtensions
    {
        /// <summary>
        /// Asynchronously fetch information for a sequence of pages.
        /// </summary>
        /// <param name="pages">A sequence of pages to be refreshed.</param>
        /// <remarks>
        /// It's recommended that <paramref name="pages"/> is a list or a subset of a list
        /// that is hold by caller, beccause this method will not return the refreshed pages.
        /// </remarks>
        public static Task RefreshAsync(this IEnumerable<Page> pages)
        {
            return RefreshAsync(pages, PageQueryOptions.None);
        }

        /// <summary>
        /// Asynchronously fetch information for a sequence of pages.
        /// </summary>
        /// <param name="pages">A sequence of pages to be refreshed.</param>
        /// <param name="options">Provides options when performing the query.</param>
        /// <remarks>
        /// It's recommended that <paramref name="pages"/> is a list or a subset of a list
        /// that is hold by caller, because this method will not return the refreshed pages.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Circular redirect detected when resolving redirects.</exception>
        public static Task RefreshAsync(this IEnumerable<Page> pages, PageQueryOptions options)
        {
            return RefreshAsync(pages, options, new CancellationToken());
        }

        /// <summary>
        /// Asynchronously fetch information for a sequence of pages.
        /// </summary>
        /// <param name="pages">A sequence of pages to be refreshed.</param>
        /// <param name="options">Provides options when performing the query.</param>
        /// <param name="cancellationToken">The cancellation token that will be checked prior to completing the returned task.</param>
        /// <remarks>
        /// It's recommended that <paramref name="pages"/> is a list or a subset of a list
        /// that is hold by caller, because this method will not return the refreshed pages.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Circular redirect detected when resolving redirects.</exception>
        public static Task RefreshAsync(this IEnumerable<Page> pages, PageQueryOptions options, CancellationToken cancellationToken)
        {
            return RequestManager.RefreshPagesAsync(pages, options, cancellationToken);
        }

        /// <summary>
        /// Asynchronously purges a sequence of pages.
        /// </summary>
        /// <returns>A collection of pages that haven't been successfully purged, because of either missing or invalid titles.</returns>
        public static Task<IReadOnlyCollection<Page>> PurgeAsync(this IEnumerable<Page> pages)
        {
            return PurgeAsync(pages, PagePurgeOptions.None, CancellationToken.None);
        }

        /// <summary>
        /// Asynchronously purges a sequence of pages with the given options.
        /// </summary>
        /// <returns>A collection of pages that haven't been successfully purged, because of either missing or invalid titles.</returns>
        public static Task<IReadOnlyCollection<Page>> PurgeAsync(this IEnumerable<Page> pages,
            PagePurgeOptions options)
        {
            return PurgeAsync(pages, options, new CancellationToken());
        }

        /// <summary>
        /// Asynchronously purges a sequence of pages with the given options.
        /// </summary>
        /// <returns>A collection of pages that haven't been successfully purged, because of either missing or invalid titles.</returns>
        public static Task<IReadOnlyCollection<Page>> PurgeAsync(this IEnumerable<Page> pages,
            PagePurgeOptions options, CancellationToken cancellationToken)
        {
            return RequestManager.PurgePagesAsync(pages, options, cancellationToken);
        }
    }
}
