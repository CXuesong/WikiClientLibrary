using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;
using WikiClientLibrary.Cargo.Linq.IntermediateExpressions;

namespace WikiClientLibrary.Cargo.Linq.ExpressionVisitors
{

    public class CargoPostEvaluationTranslator : ExpressionVisitor
    {
        /// <inheritdoc />
        protected override Expression VisitBinary(BinaryExpression node)
        {
            // Some operators are interpreted as function call.
            var ltype = node.Left.Type;
            var rtype = node.Right.Type;
            if ((ltype == typeof(DateTime) || ltype == typeof(DateTimeOffset)) && rtype == typeof(TimeSpan))
            {
                switch (node.NodeType)
                {
                    case ExpressionType.Add:
                        return new CargoFunctionExpression("DATE_ADD", ltype, Visit(node.Left), Visit(node.Right));
                    case ExpressionType.Subtract:
                        return new CargoFunctionExpression("DATE_SUB", ltype, Visit(node.Left), Visit(node.Right));
                }
            }
            return base.VisitBinary(node);
        }

        /// <inheritdoc />
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            Exception GetOverloadNotSupportedException()
                => new NotSupportedException($"Specified overload of method {node.Method.Name} is not supported.");

            if (node.Method.DeclaringType == typeof(string))
            {
                if (node.Method.IsStatic)
                {
                    switch (node.Method.Name)
                    {
                        case nameof(string.Equals):
                            return Expression.Equal(node.Arguments[0], node.Arguments[1]);
                    }
                }
                else
                {
                    Debug.Assert(node.Object != null);
                    switch (node.Method.Name)
                    {
                        case nameof(string.Equals):
                            return VisitBinary(Expression.Equal(Visit(node.Object), node.Arguments[1]));
                        case nameof(string.ToUpper):
                            return new CargoFunctionExpression("UPPER", typeof(string), Visit(node.Object));
                        case nameof(string.ToLower):
                            return new CargoFunctionExpression("LOWER", typeof(string), Visit(node.Object));
                        case nameof(string.Trim):
                            if (node.Arguments.Count > 0) throw GetOverloadNotSupportedException();
                            return new CargoFunctionExpression("TRIM", typeof(string), Visit(node.Object));
                        case nameof(string.TrimStart):
                            if (node.Arguments.Count > 0) throw GetOverloadNotSupportedException();
                            return new CargoFunctionExpression("LTRIM", typeof(string), Visit(node.Object));
                        case nameof(string.TrimEnd):
                            if (node.Arguments.Count > 0) throw GetOverloadNotSupportedException();
                            return new CargoFunctionExpression("RTRIM", typeof(string), Visit(node.Object));
                        case nameof(string.IndexOf):
                            if (node.Arguments.Count == 1)
                                return new CargoFunctionExpression("INSTR", typeof(int), Visit(node.Object), Visit(node.Arguments[0]));
                            if (node.Arguments.Count == 2 && node.Method.GetParameters()[1].ParameterType == typeof(int))
                                return new CargoFunctionExpression("LOCATE",
                                    typeof(int), Visit(node.Object), Visit(node.Arguments[0]), Visit(node.Arguments[1]));
                            throw GetOverloadNotSupportedException();
                        case nameof(string.Contains):
                            if (node.Arguments.Count > 1) throw GetOverloadNotSupportedException();
                            return Expression.OrElse(
                                Expression.Equal(node.Arguments[0], Expression.Constant("")),
                                Expression.GreaterThanOrEqual(new CargoFunctionExpression("INSTR", typeof(int), Visit(node.Object), Visit(node.Arguments[0])),
                                    Expression.Constant(0))
                            );
                        case nameof(string.Substring):
                            if (node.Arguments.Count == 1)
                                return new CargoFunctionExpression("SUBSTRING", typeof(string), Visit(node.Object), Visit(node.Arguments[1]));
                            if (node.Arguments.Count == 2)
                                return new CargoFunctionExpression("SUBSTRING",
                                    typeof(string), Visit(node.Object), Visit(node.Arguments[1]), Visit(node.Arguments[2]));
                            throw GetOverloadNotSupportedException();
                    }
                }
            }
            else if (node.Method.DeclaringType == typeof(Math)
#if BCL_FEATURE_MATHF
                     || node.Method.DeclaringType == typeof(MathF)
#endif
            )
            {
                switch (node.Method.Name)
                {
                    case nameof(Math.Floor):
                        return new CargoFunctionExpression("FLOOR", node.Method.ReturnType, Visit(node.Arguments[0]));
                    case nameof(Math.Ceiling):
                        return new CargoFunctionExpression("CEIL", node.Method.ReturnType, Visit(node.Arguments[0]));
                    case nameof(Math.Round):
                        if (node.Arguments.Count == 1)
                            return new CargoFunctionExpression("ROUND", node.Method.ReturnType, Visit(node.Arguments[0]));
                        if (node.Arguments.Count == 2 && node.Method.GetParameters()[1].ParameterType == typeof(int))
                            return new CargoFunctionExpression("ROUND", node.Method.ReturnType, Visit(node.Arguments[0]), Visit(node.Arguments[1]));
                        throw GetOverloadNotSupportedException();
                    case nameof(Math.Pow):
                        // POW is not supported by Cargo
                        return new CargoFunctionExpression("POWER", node.Method.ReturnType, Visit(node.Arguments[0]), Visit(node.Arguments[1]));
                    case nameof(Math.Exp):
                        // EXP is not supported by Cargo
                        return new CargoFunctionExpression("POWER", node.Method.ReturnType, Expression.Constant(Math.E), Visit(node.Arguments[0]));
                    case nameof(Math.Log):
                        if (node.Arguments.Count == 1)
                            return new CargoFunctionExpression("LOG", node.Method.ReturnType, Visit(node.Arguments[0]));
                        if (node.Arguments.Count == 2)
                            return new CargoFunctionExpression("LOG", node.Method.ReturnType, Visit(node.Arguments[0]), Visit(node.Arguments[1]));
                        throw GetOverloadNotSupportedException();
                }
            }
            else if (node.Method.DeclaringType == typeof(DateTime) || node.Method.DeclaringType == typeof(DateTimeOffset))
            {
                switch (node.Method.Name)
                {
                    case nameof(DateTimeOffset.Add):
                        return new CargoFunctionExpression("DATE_ADD", node.Method.ReturnType, Visit(node.Object), Visit(node.Arguments[0]));
                    case nameof(DateTimeOffset.Subtract):
                        return new CargoFunctionExpression("DATE_SUB", node.Method.ReturnType, Visit(node.Object), Visit(node.Arguments[0]));
                }
            }
            return base.VisitMethodCall(node);
        }

        /// <inheritdoc />
        protected override Expression VisitMember(MemberExpression node)
        {
            var declaringType = node.Member.DeclaringType;
            if (declaringType == typeof(string))
            {
                switch (node.Member.Name)
                {
                    case nameof(string.Length):
                        return new CargoFunctionExpression("LEN", typeof(string), Visit(node.Expression));
                }
            }
            else if (declaringType == typeof(DateTime) || declaringType == typeof(DateTimeOffset))
            {
                switch (node.Member.Name)
                {
                    case nameof(DateTimeOffset.Year):
                        return new CargoFunctionExpression("YEAR", typeof(int), Visit(node.Expression));
                    case nameof(DateTimeOffset.Month):
                        return new CargoFunctionExpression("MONTH", typeof(int), Visit(node.Expression));
                    case nameof(DateTimeOffset.Day):
                        return new CargoFunctionExpression("DAYOFMONTH", typeof(int), Visit(node.Expression));
                    case nameof(DateTimeOffset.Date):
                        return new CargoFunctionExpression("YEAR", typeof(DateTime), Visit(node.Expression));
                }
            }
            return base.VisitMember(node);
        }

    }

}
