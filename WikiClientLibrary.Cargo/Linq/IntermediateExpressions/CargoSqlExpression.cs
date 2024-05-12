using System.Linq.Expressions;

namespace WikiClientLibrary.Cargo.Linq.IntermediateExpressions;

/// <summary>
/// Base expression type for all the SQL expression segment that can be directly translated into Cargo query parameter.
/// </summary>
internal abstract class CargoSqlExpression : Expression
{

    /// <inheritdoc />
    public override bool CanReduce => false;

    /// <inheritdoc />
    public override ExpressionType NodeType => ExpressionType.Extension;

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor) => this;

}
