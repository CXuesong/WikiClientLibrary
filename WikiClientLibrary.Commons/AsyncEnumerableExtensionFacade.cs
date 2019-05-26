using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// Use WikiClientLibrary instead of WikiClientLibrary.Commons to let compiler prefer extension methods
// defined in this file when there is ambiguation resolving the extension method.
namespace WikiClientLibrary
{

    /// <summary>
    /// This class provides facade extension methods from System.Linq.AsyncEnumerable static class.
    /// Declare this class as internal and sharing the source file instead of exposing the class
    /// to prevent polluting user's observed extension methods.
    /// </summary>
    internal static class AsyncEnumerableExtensionFacade
    {

#if BCL_FEATURE_ASYNC_ENUMERABLE
        // System.Runtime

        public static Task<TSource> FirstAsync<TSource>(this IAsyncEnumerable<TSource> source, CancellationToken cancellationToken = default)
            => AsyncEnumerable.FirstAsync(source, cancellationToken).AsTask();

        public static Task<TSource> FirstOrDefaultAsync<TSource>(this IAsyncEnumerable<TSource> source, CancellationToken cancellationToken = default)
            => AsyncEnumerable.FirstOrDefaultAsync(source, cancellationToken).AsTask();

        public static Task<bool> AnyAsync<TSource>(this IAsyncEnumerable<TSource> source, CancellationToken cancellationToken = default)
            => AsyncEnumerable.AnyAsync(source, cancellationToken).AsTask();

        public static Task<bool> AnyAsync<TSource>(this IAsyncEnumerable<TSource> source, Func<TSource, bool> predicate, CancellationToken cancellationToken = default)
            => AsyncEnumerable.AnyAsync(source, predicate, cancellationToken).AsTask();

        public static Task<TSource[]> ToArrayAsync<TSource>(this IAsyncEnumerable<TSource> source, CancellationToken cancellationToken = default)
            => AsyncEnumerable.ToArrayAsync(source, cancellationToken).AsTask();

        public static Task<List<TSource>> ToListAsync<TSource>(this IAsyncEnumerable<TSource> source, CancellationToken cancellationToken = default)
            => AsyncEnumerable.ToListAsync(source, cancellationToken).AsTask();

#else
        // Ix.Async

        public static Task<TSource> FirstAsync<TSource>(this IAsyncEnumerable<TSource> source, CancellationToken cancellationToken = default)
            => AsyncEnumerable.First(source, cancellationToken);

        public static Task<TSource> FirstOrDefaultAsync<TSource>(this IAsyncEnumerable<TSource> source, CancellationToken cancellationToken = default)
            => AsyncEnumerable.FirstOrDefault(source, cancellationToken);

        public static Task<bool> AnyAsync<TSource>(this IAsyncEnumerable<TSource> source, CancellationToken cancellationToken = default)
            => AsyncEnumerable.Any(source, cancellationToken);

        public static Task<bool> AnyAsync<TSource>(this IAsyncEnumerable<TSource> source, Func<TSource, bool> predicate, CancellationToken cancellationToken = default)
            => AsyncEnumerable.Any(source, predicate, cancellationToken);

        public static Task<TSource[]> ToArrayAsync<TSource>(this IAsyncEnumerable<TSource> source, CancellationToken cancellationToken = default)
            => AsyncEnumerable.ToArray(source, cancellationToken);

        public static Task<List<TSource>> ToListAsync<TSource>(this IAsyncEnumerable<TSource> source, CancellationToken cancellationToken = default)
            => AsyncEnumerable.ToList(source, cancellationToken);

#endif

    }
}
