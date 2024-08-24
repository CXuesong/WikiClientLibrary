using System.Collections;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using WikiClientLibrary.Cargo.Linq.ExpressionVisitors;
using WikiClientLibrary.Cargo.Linq.IntermediateExpressions;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Cargo.Linq;

internal abstract class CargoRecordQueryable
{

    private CargoQueryExpression? _reducedQueryExpression;

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
                throw new InvalidOperationException(
                    $"Cannot reduce the expression to CargoQueryExpression. Actual type: {expr?.GetType()}.");
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

    private async IAsyncEnumerable<T> BuildAsyncEnumerable([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var queryParams = BuildQueryParameters();
        var limit = queryParams.Limit;
        // Trivial case
        if (limit == 0) yield break;
        var paginationSize = Provider.PaginationSize;
        // Restrict Limit to pagination size.
        if (queryParams.Limit < 0 || queryParams.Limit > paginationSize)
            queryParams.Limit = paginationSize;
        var queryExpr = this.ReducedQueryExpression;
        var yieldedCount = 0;
        queryParams.Offset = 0;
        while (queryParams.Limit > 0)
        {
            var result = await Provider.WikiSite.ExecuteCargoQueryAsync(queryParams, cancellationToken);
            // No more results.
            if (result.Count == 0) yield break;
            using (ExecutionContextStash.Capture())
                foreach (var r in result)
                {
                    yield return (T)Provider.RecordConverter.DeserializeRecord(
                        r.Select(p => (proj: queryExpr.TryGetProjectionByAlias(p.Key), value: p.Value))
                            .Where(t => t.proj != null)
                            .ToDictionary(
                                t => t.proj!.TargetMember,
                                t => t.value!
                            ), typeof(T));
                }
            yieldedCount += result.Count;
            // Prepare for next batch.
            queryParams.Offset += result.Count;
            queryParams.Limit = limit < 0 ? paginationSize : Math.Min(paginationSize, limit - yieldedCount);
        }
    }

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => BuildAsyncEnumerable().ToEnumerable().GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => BuildAsyncEnumerable().ToEnumerable().GetEnumerator();

    /// <inheritdoc />
    IQueryProvider IQueryable.Provider => Provider;

    /// <inheritdoc />
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        BuildAsyncEnumerable(cancellationToken).GetAsyncEnumerator(cancellationToken);

}
