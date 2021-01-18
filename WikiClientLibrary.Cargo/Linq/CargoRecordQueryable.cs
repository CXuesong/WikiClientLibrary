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
            expr = new CargoPreEvaluationTranslator().VisitAndConvert(expr, nameof(BuildQueryParameters));
            expr = new ExpressionTreePartialEvaluator().VisitAndConvert(expr, nameof(BuildQueryParameters));
            expr = new CargoPostEvaluationTranslator().VisitAndConvert(expr, nameof(BuildQueryParameters));
            expr = new CargoQueryExpressionReducer().VisitAndConvert(expr, nameof(BuildQueryParameters));
            if (expr is CargoQueryExpression cqe)
            {
                var cb = new CargoQueryClauseBuilder();
                var p = new CargoQueryParameters
                {
                    Fields = cqe.Fields.Select(f => cb.BuildClause(f)).ToList(),
                    Tables = cqe.Tables.Select(t => cb.BuildClause(t)).ToList(),
                    Where = cqe.Predicate == null ? null : cb.BuildClause(cqe.Predicate),
                    OrderBy = cqe.OrderBy.Select(f => cb.BuildClause(f)).ToList(),
                    Offset = cqe.Offset,
                    // We use -1 to let caller know we shouldn't limit record count.
                    Limit = cqe.Limit ?? -1,
                };
                return p;
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

        private IAsyncEnumerable<T> BuildAsyncEnumerable()
        {
            var p = BuildQueryParameters();
            var limit = p.Limit;
            // Trivial case
            if (limit == 0) return AsyncEnumerable.Empty<T>();
            var paginationSize = Provider.PaginationSize;
            // Restrict Limit to pagination size.
            if (p.Limit < 0 || p.Limit > paginationSize)
                p.Limit = paginationSize;
            return AsyncEnumerableFactory.FromAsyncGenerator<T>(async (sink, ct) =>
            {
                var yieldedCount = 0;
                p.Offset = 0;
                while (p.Limit > 0)
                {
                    var result = await Provider.WikiSite.ExecuteCargoQueryAsync(p, ct);
                    // No more record.
                    if (result.Count == 0) return;
                    await sink.YieldAndWait(result.Select(r => r.ToObject<T>()));
                    yieldedCount += result.Count;
                    // Prepare for next batch.
                    p.Offset += result.Count;
                    p.Limit = limit < 0 ? paginationSize : Math.Min(paginationSize, limit - yieldedCount);
                }
            });
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
