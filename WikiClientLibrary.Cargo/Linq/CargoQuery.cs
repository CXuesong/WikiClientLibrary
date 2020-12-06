using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using AsyncEnumerableExtensions;

namespace WikiClientLibrary.Cargo.Linq
{

    internal abstract class CargoQuery
    {

        internal CargoQuery(CargoQueryProvider provider, Expression expression, Type elementType)
        {
            Debug.Assert(provider != null);
            Debug.Assert(expression != null);
            Debug.Assert(elementType != null);
            Provider = provider;
            Expression = expression;
            ElementType = elementType;
        }

        public Type ElementType { get; }

        public Expression Expression { get; }

        public CargoQueryProvider Provider { get; }

    }

    internal class CargoQuery<T> : CargoQuery, IQueryable<T>, IAsyncEnumerable<T>
    {

        internal CargoQuery(CargoQueryProvider provider, Expression expression)
            : base(provider, expression, typeof(T))
        {
        }

        public CargoQueryParameters BuildQueryParameters()
        {
            return new CargoQueryParameters();
        }

        /// <inheritdoc />
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => BuildAsyncEnumerable().ToEnumerable().GetEnumerator();

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => BuildAsyncEnumerable().ToEnumerable().GetEnumerator();

        /// <inheritdoc />
        IQueryProvider IQueryable.Provider => Provider;

        private IAsyncEnumerable<T> BuildAsyncEnumerable() =>
            AsyncEnumerableFactory.FromAsyncGenerator<T>(async (sink, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                var p = BuildQueryParameters();
                var result = await Provider.WikiSite.ExecuteCargoQueryAsync(p, ct);
                await sink.YieldAndWait(result.Select(r => r.ToObject<T>()));
            });

        /// <inheritdoc />
#if BCL_FEATURE_ASYNC_ENUMERABLE
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            BuildAsyncEnumerable().GetAsyncEnumerator(cancellationToken);
#else
        public IAsyncEnumerator<T> GetEnumerator()
            => BuildAsyncEnumerable().GetEnumerator();
#endif

    }

}
