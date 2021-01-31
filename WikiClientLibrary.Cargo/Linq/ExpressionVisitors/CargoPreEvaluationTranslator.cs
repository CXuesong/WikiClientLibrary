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
                switch (node.Method.Name)
                {
                    case nameof(CargoFunctions.Like):
                        return new CargoBinaryOperationExpression(" LIKE ", Visit(node.Arguments[0]), Visit(node.Arguments[1]), node.Type);
                    case nameof(CargoFunctions.Holds):
                        return new CargoBinaryOperationExpression(" HOLDS ", Visit(node.Arguments[0]), Visit(node.Arguments[1]), node.Type);
                    case nameof(CargoFunctions.HoldsLike):
                        return new CargoBinaryOperationExpression(" HOLDS LIKE ", Visit(node.Arguments[0]), Visit(node.Arguments[1]), node.Type);
                    case nameof(CargoFunctions.DateDiff):
                        return new CargoFunctionExpression("DATEDIFF", typeof(int), Visit(node.Arguments[0]), Visit(node.Arguments[1]));
                }
                throw new NotImplementedException($"CargoFunction call is not implemented: {node.Method}.");
            }
            return base.VisitMethodCall(node);
        }

        /// <inheritdoc />
        protected override Expression VisitMember(MemberExpression node)
        {
            var declaringType = node.Member.DeclaringType;
            if (node.Member.Name == nameof(Nullable<int>.Value))
            {
                var nullableUnderlyingType = Nullable.GetUnderlyingType(declaringType);
                if (nullableUnderlyingType != null)
                {
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
                        return new CargoFunctionExpression("NOW", node.Member.DeclaringType);
                }
            }
            return base.VisitMember(node);
        }

    }

}
