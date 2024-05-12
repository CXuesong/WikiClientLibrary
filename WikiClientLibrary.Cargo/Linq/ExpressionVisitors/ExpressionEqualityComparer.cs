using System.Linq.Expressions;

namespace WikiClientLibrary.Cargo.Linq.ExpressionVisitors;

internal class ExpressionEqualityComparer : EqualityComparer<Expression>
{

    public static new ExpressionEqualityComparer Default { get; } = new ExpressionEqualityComparer();

    /// <inheritdoc />
    public override bool Equals(Expression? x, Expression? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x == null || y == null) return false;
        switch (x)
        {
            case ConstantExpression cex:
                if (y is ConstantExpression cey)
                    return cex.NodeType == cey.NodeType && cex.Type == cey.Type && Equals(cex.Value, cey.Value);
                return false;
            case BinaryExpression bex:
                if (y is BinaryExpression bey)
                    return bex.NodeType == bey.NodeType && Equals(bex.Method, bey.Method) && Equals(bex.Left, bey.Left) && Equals(bex.Right, bey.Right);
                return false;
            case MemberExpression mex:
                if (y is MemberExpression mey)
                    return mex.NodeType == mey.NodeType && Equals(mex.Member, mey.Member) && mex.Type == mey.Type && Equals(mex.Expression, mey.Expression);
                return false;
        }
        return x.Equals(y);
    }

    /// <inheritdoc />
    public override int GetHashCode(Expression? obj)
    {
        return obj switch
        {
            ConstantExpression ce => HashCode.Combine(ce.NodeType, ce.Type, ce.Value),
            BinaryExpression be => HashCode.Combine(be.NodeType, GetHashCode(be.Left), GetHashCode(be.Right)),
            MemberExpression me => HashCode.Combine(me.NodeType, GetHashCode(me.Expression), me.Member, me.Type),
            null => 0,
            _ => obj.GetHashCode()
        };
    }

}