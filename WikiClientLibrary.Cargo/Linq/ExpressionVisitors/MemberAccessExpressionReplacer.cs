using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace WikiClientLibrary.Cargo.Linq.ExpressionVisitors;

/// <summary>
/// Replaces the member access (read) expression to a specified <see cref="Target"/> into the specified expression.
/// </summary>
public class MemberAccessExpressionReplacer : ExpressionVisitor
{

    public MemberAccessExpressionReplacer(ParameterExpression target, IReadOnlyDictionary<MemberInfo, Expression> memberReplacements)
    {
        Debug.Assert(target != null);
        Target = target;
        MemberReplacements = memberReplacements;
    }

    public ParameterExpression Target { get; }

    public IReadOnlyDictionary<MemberInfo, Expression> MemberReplacements { get; }

    /// <inheritdoc />
    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression == Target)
        {
            return MemberReplacements[node.Member];
        }
        return base.VisitMember(node);
    }

}
