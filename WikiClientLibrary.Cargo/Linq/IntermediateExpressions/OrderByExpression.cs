using System.Linq.Expressions;

namespace WikiClientLibrary.Cargo.Linq.IntermediateExpressions;

internal class OrderByExpression : CargoSqlExpression
{

    public OrderByExpression(Expression expression)
        : this(expression, false)
    {
    }

    public OrderByExpression(Expression expression, bool descending)
    {
        Expression = expression;
        Descending = @descending;
    }

    /// <inheritdoc />
    public override Type Type => typeof(void);

    /// <summary>Sort by this expression.</summary>
    public Expression Expression { get; }

    public bool Descending { get; }

    /// <inheritdoc />
    public override string ToString() => Expression + (Descending ? " DESC" : " ASC");

}
