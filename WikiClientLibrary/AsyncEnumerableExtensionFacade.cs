using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WikiClientLibrary
{
    internal static class AsyncEnumerableExtensionFacade
    {

#if BCL_FEATURE_ASYNC_ENUMERABLE
        // System.Runtime

        public static Task<TSource> FirstAsync<TSource>(this IAsyncEnumerable<TSource> source, CancellationToken cancellationToken = default)
            => AsyncEnumerable.FirstAsync(source, cancellationToken).AsTask();

        public static Task<bool> AnyAsync<TSource>(this IAsyncEnumerable<TSource> source, CancellationToken cancellationToken = default)
            => AsyncEnumerable.AnyAsync(source, cancellationToken).AsTask();

        public static Task<List<TSource>> ToListAsync<TSource>(this IAsyncEnumerable<TSource> source, CancellationToken cancellationToken = default)
            => AsyncEnumerable.ToListAsync(source, cancellationToken).AsTask();

#else
        // Ix.Async

        public static Task<TSource> FirstAsync<TSource>(this IAsyncEnumerable<TSource> source, CancellationToken cancellationToken = default)
            => AsyncEnumerable.First(source, cancellationToken);

        public static Task<bool> AnyAsync<TSource>(this IAsyncEnumerable<TSource> source, CancellationToken cancellationToken = default)
            => AsyncEnumerable.Any(source, cancellationToken);

        public static Task<List<TSource>> ToListAsync<TSource>(this IAsyncEnumerable<TSource> source, CancellationToken cancellationToken = default)
            => AsyncEnumerable.ToList(source, cancellationToken);

#endif

    }
}
