using System.Diagnostics;
using System.Linq.Expressions;

namespace WikiClientLibrary.Cargo.Linq.IntermediateExpressions;

internal sealed class CargoBinaryOperationExpression : CargoSqlExpression
{

    public CargoBinaryOperationExpression(string @operator, Expression left, Expression right, Type type)
    {
        Debug.Assert(@operator != null);
        Debug.Assert(left != null);
        Debug.Assert(right != null);
        Debug.Assert(type != null);
        Operator = @operator;
        Left = left;
        Right = right;
        Type = type;
    }

    public string Operator { get; }

    public Expression Left { get; }

    public Expression Right { get; }

    /// <inheritdoc />
    public override Type Type { get; }

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor)
    {
        var left = visitor.Visit(Left);
        var right = visitor.Visit(Right);
        return Update(left, right);
    }

    public CargoBinaryOperationExpression Update(Expression left, Expression right)
    {
        if (Left == left && Right == right)
            return this;
        return new CargoBinaryOperationExpression(Operator, left, right, Type);
    }
}