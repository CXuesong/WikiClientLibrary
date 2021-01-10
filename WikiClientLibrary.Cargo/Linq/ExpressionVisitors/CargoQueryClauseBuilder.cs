using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Numerics;
using System.Text;
using WikiClientLibrary.Cargo.Linq.IntermediateExpressions;

namespace WikiClientLibrary.Cargo.Linq.ExpressionVisitors
{

    // c.f. https://www.mediawiki.org/wiki/Extension:Cargo/Querying_data#Using_SQL_functions
    /// <summary>
    /// Builds clause (segment of query expression) from the expression.
    /// </summary>
    internal class CargoQueryClauseBuilder : ExpressionVisitor
    {

        private readonly StringBuilder _builder = new StringBuilder();

        // c.f. https://github.com/dotnet/efcore/blob/main/src/EFCore.Relational/Query/QuerySqlGenerator.cs
        private static readonly Dictionary<ExpressionType, string> _operatorMap = new Dictionary<ExpressionType, string>
        {
            { ExpressionType.Equal, " = " },
            { ExpressionType.NotEqual, " <> " },
            { ExpressionType.GreaterThan, " > " },
            { ExpressionType.GreaterThanOrEqual, " >= " },
            { ExpressionType.LessThan, " < " },
            { ExpressionType.LessThanOrEqual, " <= " },
            { ExpressionType.AndAlso, " AND " },
            { ExpressionType.OrElse, " OR " },
            { ExpressionType.Add, " + " },
            { ExpressionType.Subtract, " - " },
            { ExpressionType.Multiply, " * " },
            { ExpressionType.Divide, " / " },
            { ExpressionType.Modulo, " % " },
            { ExpressionType.And, " & " },
            { ExpressionType.Or, " | " }
        };

        public string BuildClause(Expression expr)
        {
            Debug.Assert(_builder.Length == 0);
            Visit(expr);
            try
            {
                return _builder.ToString();
            }
            finally
            {
                _builder.Clear();
            }
        }

        /// <inheritdoc />
        protected override Expression VisitConstant(ConstantExpression node)
        {
            var value = node.Value;
            switch (value)
            {
                case null:
#if BCL_FEATURE_DBNULL
                case DBNull _:
#endif
                    _builder.Append("NULL");
                    break;
                case string s:
                {
                    _builder.Append('\'');
                    var p1 = _builder.Length;
                    _builder.Append(s);
                    _builder.Replace("'", "''", p1, _builder.Length - p1);
                    _builder.Append('\'');
                    break;
                }
                case byte _:
                case sbyte _:
                case short _:
                case ushort _:
                case int _:
                case uint _:
                case long _:
                case ulong _:
                case float _:
                case double _:
                case decimal _:
                case BigInteger _:
                    _builder.Append(((IFormattable)value).ToString(null, CultureInfo.InvariantCulture));
                    break;
                case true:
                    _builder.Append("TRUE");
                    break;
                case false:
                    _builder.Append("FALSE");
                    break;
                case DateTime _:
                case DateTimeOffset _:
                    // ODBC format
                    _builder.AppendFormat("{{dt'{0:O}'}}", value);
                    break;
                case TimeSpan ts:
                    // https://dev.mysql.com/doc/refman/5.6/en/expressions.html#temporal-intervals
                    _builder.Append("INTERVAL ");
                    if (ts.Ticks % TimeSpan.TicksPerDay == 0)
                    {
                        _builder.Append(ts.Ticks / TimeSpan.TicksPerDay);
                        _builder.Append(" DAY");
                    }
                    else if (ts.Ticks < TimeSpan.TicksPerDay)
                    {
                        _builder.Append(ts);
                        _builder.Append(" HOUR_MICROSECOND");
                    }
                    else
                    {
                        _builder.Append(ts.Days);
                        _builder.Append(' ');
                        _builder.Append(new TimeSpan(ts.Ticks % TimeSpan.TicksPerDay));
                        _builder.Append(" DAY_MICROSECOND");
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Constant value (of {value.GetType()}) is not supported: {value}");
            }
            return base.VisitConstant(node);
        }

        /// <inheritdoc />
        protected override Expression VisitUnary(UnaryExpression node)
        {
            _builder.Append('(');
            switch (node.NodeType)
            {
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                    // We do not surface cast operators to SQL expression.
                    break;
                case ExpressionType.Not:
                    _builder.Append("NOT ");
                    break;
                default:
                    throw new InvalidOperationException($"Operator is not supported: {node.NodeType}.");
            }
            Visit(node.Operand);
            _builder.Append(')');
            return node;
        }

        /// <inheritdoc />
        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (!_operatorMap.TryGetValue(node.NodeType, out var op))
                throw new InvalidOperationException($"Operator is not supported: {node.NodeType}.");
            // Some operators are interpreted as function call.
            var ltype = node.Left.Type;
            var rtype = node.Right.Type;
            if (ltype == typeof(DateTime) || ltype == typeof(DateTimeOffset))
            {
                switch (node.NodeType)
                {
                    case ExpressionType.Add when rtype == typeof(TimeSpan):
                        BuildFunctionCall("DATE_ADD", node.Left, node.Right);
                        return node;
                    case ExpressionType.Subtract when rtype == typeof(TimeSpan):
                        BuildFunctionCall("DATE_SUB", node.Left, node.Right);
                        return node;
                }
            }
            // Unconditionally add brackets to ensure priority is correct.
            _builder.Append('(');
            Visit(node.Left);
            if ((node.NodeType == ExpressionType.Equal || node.NodeType == ExpressionType.NotEqual)
                && (node.Right is ConstantExpression c2 && c2.Value == null
                    || node.Left is ConstantExpression c1 && c1.Value == null))
            {
                // use `IS NULL` instead of `= NULL`.
                op = node.NodeType == ExpressionType.Equal ? " IS " : " IS NOT ";
            }
            _builder.Append(op);
            Visit(node.Right);
            _builder.Append(')');
            return node;
        }

        /// <inheritdoc />
        protected override Expression VisitExtension(Expression node)
        {
            switch (node)
            {
                case FieldRefExpression fr:
                    _builder.Append(fr.TableAlias);
                    _builder.Append('.');
                    _builder.Append(fr.FieldName);
                    break;
                case OrderByExpression ob:
                    Visit(ob.Expression);
                    _builder.Append(ob.Descending ? " DESC" : " ASC");
                    break;
                case ProjectionExpression proj:
                    Visit(proj.Expression);
                    _builder.Append(" = ");
                    _builder.Append(proj.Alias);
                    break;
                case TableProjectionExpression tp:
                    _builder.Append(tp.TableName);
                    _builder.Append(" = ");
                    _builder.Append(tp.TableAlias);
                    break;
                default:
                    throw new InvalidOperationException($"ExtensionExpression is not supported: {node.GetType()}.");
            }
            return node;
        }

        private void BuildBinaryOperation(string op, Expression left, Expression right)
        {
            _builder.Append('(');
            Visit(left);
            _builder.Append(op);
            Visit(right);
            _builder.Append(')');
        }

        private void BuildFunctionCall(string name)
        {
            _builder.Append(name);
            _builder.Append("()");
        }

        private void BuildFunctionCall(string name, Expression arg0)
        {
            _builder.Append(name);
            _builder.Append('(');
            Visit(arg0);
            _builder.Append(')');
        }

        private void BuildFunctionCall(string name, Expression arg0, Expression arg1)
        {
            _builder.Append(name);
            _builder.Append('(');
            Visit(arg0);
            _builder.Append(',');
            Visit(arg1);
            _builder.Append(')');
        }

        private void BuildFunctionCall(string name, Expression arg0, Expression arg1, Expression arg2)
        {
            _builder.Append(name);
            _builder.Append('(');
            Visit(arg0);
            _builder.Append(',');
            Visit(arg1);
            _builder.Append(',');
            Visit(arg2);
            _builder.Append(')');
        }

        /// <inheritdoc />
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            Exception GetOverloadNotSupportedException()
                => new NotSupportedException($"Specified overload of method {node.Method.Name} is not supported.");

            if (node.Object == null && node.Method.DeclaringType == typeof(CargoFunctions))
            {
                switch (node.Method.Name)
                {
                    case nameof(CargoFunctions.Like):
                        BuildBinaryOperation(" LIKE ", node.Arguments[0], node.Arguments[1]);
                        return node;
                    case nameof(CargoFunctions.Holds):
                        BuildBinaryOperation(" HOLDS ", node.Arguments[0], node.Arguments[1]);
                        return node;
                    case nameof(CargoFunctions.HoldsLike):
                        BuildBinaryOperation(" HOLDS LIKE ", node.Arguments[0], node.Arguments[1]);
                        return node;
                }
            }
            else if (node.Method.DeclaringType == typeof(string))
            {
                if (node.Method.IsStatic)
                {
                    switch (node.Method.Name)
                    {
                        case nameof(string.Equals):
                            return VisitBinary(Expression.Equal(node.Arguments[0], node.Arguments[1]));
                    }
                }
                else
                {
                    Debug.Assert(node.Object != null);
                    switch (node.Method.Name)
                    {
                        case nameof(string.Equals):
                            return VisitBinary(Expression.Equal(node.Object, node.Arguments[1]));
                        case nameof(string.ToUpper):
                            BuildFunctionCall("UPPER", node.Object);
                            return node;
                        case nameof(string.ToLower):
                            BuildFunctionCall("LOWER", node.Object);
                            return node;
                        case nameof(string.Trim):
                            if (node.Arguments.Count > 0) throw GetOverloadNotSupportedException();
                            BuildFunctionCall("TRIM", node.Object);
                            return node;
                        case nameof(string.TrimStart):
                            if (node.Arguments.Count > 0) throw GetOverloadNotSupportedException();
                            BuildFunctionCall("LTRIM", node.Object);
                            return node;
                        case nameof(string.TrimEnd):
                            if (node.Arguments.Count > 0) throw GetOverloadNotSupportedException();
                            BuildFunctionCall("RTRIM", node.Object);
                            return node;
                        case nameof(string.Contains):
                            if (node.Arguments.Count > 1) throw GetOverloadNotSupportedException();
                            _builder.Append('(');
                            Visit(node.Arguments[0]);
                            _builder.Append(" = '' OR ");
                            BuildFunctionCall("INSTR", node.Object, node.Arguments[0]);
                            _builder.Append(" >= 0)");
                            return node;
                        case nameof(string.Substring):
                            if (node.Arguments.Count < 2) throw GetOverloadNotSupportedException();
                            BuildFunctionCall("SUBSTRING", node.Object, node.Arguments[1], node.Arguments[2]);
                            return node;
                    }
                }
            }
            else if (node.Method.DeclaringType == typeof(DateTime) || node.Method.DeclaringType == typeof(DateTimeOffset))
            {
                switch (node.Method.Name)
                {
                    case nameof(DateTimeOffset.Add):
                        BuildFunctionCall("DATE_ADD", node.Object, node.Arguments[0]);
                        return node;
                    case nameof(DateTimeOffset.Subtract) when node.Arguments[0].Type == typeof(TimeSpan):
                        BuildFunctionCall("DATE_SUB", node.Object, node.Arguments[0]);
                        return node;
                }
            }
            throw new NotSupportedException($"Translation of method invocation to {node.Method} is not supported.");
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
                    // Build `field.Value` as `field`, lifting the nullability.
                    return Visit(node.Expression);
                }
            }
            if (declaringType == typeof(string))
            {
                switch (node.Member.Name)
                {
                    case nameof(string.Length):
                        BuildFunctionCall("LEN", node.Expression);
                        return node;
                }
            }
            else if (declaringType == typeof(DateTime) || declaringType == typeof(DateTimeOffset))
            {
                switch (node.Member.Name)
                {
                    case nameof(DateTimeOffset.Now):
                        BuildFunctionCall("NOW");
                        return node;
                    case nameof(DateTimeOffset.Year):
                        BuildFunctionCall("YEAR", node.Expression);
                        return node;
                    case nameof(DateTimeOffset.Month):
                        BuildFunctionCall("MONTH", node.Expression);
                        return node;
                    case nameof(DateTimeOffset.Day):
                        BuildFunctionCall("DAYOFMONTH", node.Expression);
                        return node;
                    case nameof(DateTimeOffset.Date):
                        BuildFunctionCall("DATE", node.Expression);
                        return node;
                }
            }
            throw new NotSupportedException($"Translation of member access to {node.Member} is not supported.");
        }
    }

}
