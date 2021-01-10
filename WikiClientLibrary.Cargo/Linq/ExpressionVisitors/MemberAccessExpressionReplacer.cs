using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace WikiClientLibrary.Cargo.Linq.ExpressionVisitors
{

    /// <summary>
    /// Replaces the member access (read) expression to a specified <see cref="Target"/> into the specified expression.
    /// </summary>
    public class MemberAccessExpressionReplacer : ExpressionVisitor
    {

        public MemberAccessExpressionReplacer(ParameterExpression target, IReadOnlyDictionary<string, Expression> memberReplacements)
        {
            Debug.Assert(target != null);
            Target = target;
            MemberReplacements = memberReplacements;
        }

        public ParameterExpression Target { get; }

        public IReadOnlyDictionary<string, Expression> MemberReplacements { get; }

        /// <inheritdoc />
        protected override Expression VisitMember(MemberExpression node)
        {
            var visitedNode = base.VisitMember(node);
            Debug.Assert(ReferenceEquals(visitedNode, node));

            if (node.Expression == Target)
            {
                var columnName = CargoModelUtility.ColumnNameFromProperty(node.Member);
                return MemberReplacements[columnName];
            }
            return visitedNode;
        }

    }

}
