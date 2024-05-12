namespace WikiClientLibrary.Cargo.Linq;

public static class CargoRecordQueryableExtensions
{

    /// <summary>
    /// Casts the input <see cref="IQueryable{T}"/> instance into <see cref="IAsyncEnumerable{T}"/> instance.
    /// </summary>
    /// <typeparam name="T">type of the item.</typeparam>
    /// <param name="queryable">the input queryable that should have implemented <see cref="IAsyncEnumerable{T}"/>.</param>
    /// <exception cref="InvalidOperationException">input <paramref name="queryable"/> does not implement <see cref="IAsyncEnumerable{T}"/>.</exception>
    /// <returns>the same object reference as <paramref name="queryable"/>.</returns>
    /// <remarks>This method is expected to have exactly the same behavior as <see cref="T:Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsAsyncEnumerable"/>.</remarks>
    public static IAsyncEnumerable<T> AsAsyncEnumerable<T>(this IQueryable<T> queryable)
    {
        return queryable is IAsyncEnumerable<T> asyncEnumerable
            ? asyncEnumerable
            : throw new InvalidOperationException("Input IQueryable does not implement IAsyncEnumerable.");
    }

}