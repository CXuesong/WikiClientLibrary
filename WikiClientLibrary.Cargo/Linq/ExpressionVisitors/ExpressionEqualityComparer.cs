using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace WikiClientLibrary.Cargo.Linq.ExpressionVisitors
{
    internal class ExpressionEqualityComparer : EqualityComparer<Expression>
    {

        public static new ExpressionEqualityComparer Default { get; } = new ExpressionEqualityComparer();

        /// <inheritdoc />
        public override bool Equals(Expression x, Expression y)
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
        public override int GetHashCode(Expression obj)
        {
            switch (obj)
            {
                case ConstantExpression ce:
                    return unchecked(ce.NodeType.GetHashCode() * 7 + ce.Type.GetHashCode() * 43 + (ce.Value == null ? 0 : ce.Value.GetHashCode()) * 71);
                case BinaryExpression be:
                    return unchecked(be.NodeType.GetHashCode() * 7 + GetHashCode(be.Left) * 43 + GetHashCode(be.Right) * 71);
                case MemberExpression me:
                    return unchecked(me.NodeType.GetHashCode() * 7 + GetHashCode(me.Expression) * 43 + me.Member.GetHashCode() * 71 +
                                     me.Type.GetHashCode() * 97);
                case null:
                    return 0;
            }
            return obj.GetHashCode();
        }

    }
}
