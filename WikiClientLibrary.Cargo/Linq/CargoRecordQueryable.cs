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

    internal abstract class CargoRecordQueryable
    {

        private CargoQueryExpression _reducedQueryExpression;

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

        /// <summary>Gets the reduced query expression.</summary>
        protected CargoQueryExpression ReducedQueryExpression
        {
            get
            {
                var queryExpr = Volatile.Read(ref _reducedQueryExpression);
                if (queryExpr != null) return queryExpr;
                var expr = Expression;
                expr = new CargoPreEvaluationTranslator().VisitAndConvert(expr, nameof(BuildQueryParameters));
                expr = new ExpressionTreePartialEvaluator().VisitAndConvert(expr, nameof(BuildQueryParameters));
                expr = new CargoPostEvaluationTranslator().VisitAndConvert(expr, nameof(BuildQueryParameters));
                expr = new CargoQueryExpressionReducer().VisitAndConvert(expr, nameof(BuildQueryParameters));
                queryExpr = expr as CargoQueryExpression;
                if (queryExpr == null)
                    throw new InvalidOperationException($"Cannot reduce the expression to CargoQueryExpression. Actual type: {expr?.GetType()}.");
                return Interlocked.CompareExchange(ref _reducedQueryExpression, queryExpr, null) ?? queryExpr;
            }
        }

        /// <summary>
        /// Builds Cargo API query parameters from the current LINQ to Cargo expression.
        /// </summary>
        public CargoQueryParameters BuildQueryParameters()
        {
            var cqe = ReducedQueryExpression;
            var cb = new CargoQueryClauseBuilder();
            var p = new CargoQueryParameters
            {
                Fields = cqe.Fields.Select(f => cb.BuildClause(f)).ToList(),
                Tables = cqe.Tables.Select(t => cb.BuildClause(t)).ToList(),
                Where = cqe.Predicate == null ? null : cb.BuildClause(cqe.Predicate),
                OrderBy = cqe.OrderBy.Select(f => cb.BuildClause(f)).ToList(),
                Offset = cqe.Offset,
                // We use -1 to let caller know this query is not limiting record count.
                Limit = cqe.Limit ?? -1,
            };
            return p;
        }

    }

    internal class CargoRecordQueryable<T> : CargoRecordQueryable, IQueryable<T>, IOrderedQueryable<T>, IAsyncEnumerable<T>
    {

        internal CargoRecordQueryable(CargoQueryProvider provider, Expression expression)
            : base(provider, expression, typeof(T))
        {
        }

        private IAsyncEnumerable<T> BuildAsyncEnumerable()
        {
            var queryParams = BuildQueryParameters();
            var limit = queryParams.Limit;
            // Trivial case
            if (limit == 0) return AsyncEnumerable.Empty<T>();
            var paginationSize = Provider.PaginationSize;
            // Restrict Limit to pagination size.
            if (queryParams.Limit < 0 || queryParams.Limit > paginationSize)
                queryParams.Limit = paginationSize;
            return AsyncEnumerableFactory.FromAsyncGenerator<T>(async (sink, ct) =>
            {
                var queryExpr = this.ReducedQueryExpression;
                var yieldedCount = 0;
                queryParams.Offset = 0;
                while (queryParams.Limit > 0)
                {
                    var result = await Provider.WikiSite.ExecuteCargoQueryAsync(queryParams, ct);
                    // No more record.
                    if (result.Count == 0) return;
                    await sink.YieldAndWait(result.Select(r => (T)Provider.RecordConverter.DeserializeRecord(
                        r.Properties().Select(p => (proj: queryExpr.TryGetProjectionByAlias(p.Name), value: p.Value))
                            .Where(t => t.proj != null)
                            .ToDictionary(
                                t => t.proj.TargetMember,
                                t => t.value
                            ), typeof(T))));
                    yieldedCount += result.Count;
                    // Prepare for next batch.
                    queryParams.Offset += result.Count;
                    queryParams.Limit = limit < 0 ? paginationSize : Math.Min(paginationSize, limit - yieldedCount);
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
