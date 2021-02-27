using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;
using WikiClientLibrary.Cargo.Linq.IntermediateExpressions;

namespace WikiClientLibrary.Cargo.Linq.ExpressionVisitors
{

    public class CargoPreEvaluationTranslator : ExpressionVisitor
    {

        /// <inheritdoc />
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Object == null && node.Method.DeclaringType == typeof(CargoFunctions))
            {
                return node.Method.Name switch
                {
                    nameof(CargoFunctions.Like) => new CargoBinaryOperationExpression(" LIKE ", Visit(node.Arguments[0]), Visit(node.Arguments[1]), node.Type),
                    nameof(CargoFunctions.Holds) =>
                        new CargoBinaryOperationExpression(" HOLDS ", Visit(node.Arguments[0]), Visit(node.Arguments[1]), node.Type),
                    nameof(CargoFunctions.HoldsLike) => new CargoBinaryOperationExpression(" HOLDS LIKE ", Visit(node.Arguments[0]), Visit(node.Arguments[1]),
                        node.Type),
                    nameof(CargoFunctions.DateDiff) => new CargoFunctionExpression("DATEDIFF", typeof(int), Visit(node.Arguments[0]), Visit(node.Arguments[1])),
                    _ => throw new NotImplementedException($"CargoFunction call is not implemented: {node.Method}.")
                };
            }
            return base.VisitMethodCall(node);
        }

        /// <inheritdoc />
        protected override Expression VisitMember(MemberExpression node)
        {
            var declaringType = node.Member.DeclaringType;
            if (declaringType == null) return base.VisitMember(node);
            if (node.Member.Name == nameof(Nullable<int>.Value))
            {
                var nullableUnderlyingType = Nullable.GetUnderlyingType(declaringType);
                if (nullableUnderlyingType != null)
                {
                    Debug.Assert(node.Expression != null);
                    // Normalize `field.Value` into `(T)field`.
                    return Expression.Convert(node.Expression, nullableUnderlyingType);
                }
            }
            else if (declaringType == typeof(DateTime) || declaringType == typeof(DateTimeOffset))
            {
                switch (node.Member.Name)
                {
                    case nameof(DateTimeOffset.Now):
                        // Converts "NOW" as NOW() server-side function call.
                        return new CargoFunctionExpression("NOW", node.Member.DeclaringType!);
                }
            }
            return base.VisitMember(node);
        }

    }

}
