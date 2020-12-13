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
using WikiClientLibrary.Cargo.Linq.ExpressionVisitors;
using WikiClientLibrary.Cargo.Linq.IntermediateExpressions;

namespace WikiClientLibrary.Cargo.Linq
{

    public abstract class CargoRecordQueryable
    {

        internal CargoRecordQueryable(CargoQueryProvider provider, Expression expression, Type elementType)
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

        /// <summary>
        /// Builds Cargo API query parameters from the current LINQ to Cargo expression.
        /// </summary>
        public CargoQueryParameters BuildQueryParameters()
        {
            var expr = Expression;
            expr = new ExpressionTreePartialEvaluator().VisitAndConvert(expr, nameof(BuildQueryParameters));
            expr = new CargoQueryExpressionReducer().VisitAndConvert(expr, nameof(BuildQueryParameters));
            if (expr is CargoQueryExpression cqe)
            {
                // TODO
                return new CargoQueryParameters();
            }
            throw new InvalidOperationException($"Cannot reduce the expression to CargoQueryExpression. Actual type: {expr?.GetType()}.");
        }

    }

    public class CargoRecordQueryable<T> : CargoRecordQueryable, IQueryable<T>, IOrderedQueryable<T>, IAsyncEnumerable<T>
    {

        internal CargoRecordQueryable(CargoQueryProvider provider, Expression expression)
            : base(provider, expression, typeof(T))
        {
        }

        /// <inheritdoc />
#if BCL_FEATURE_ASYNC_ENUMERABLE
        public IEnumerator<T> GetEnumerator() => BuildAsyncEnumerable().ToEnumerable().GetEnumerator();
#else
        // Use explicit implementation on .NET Standard 1.1 to avoid name conflict with the one returning IAsyncEnumerator.
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => BuildAsyncEnumerable().ToEnumerable().GetEnumerator();
#endif

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
