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

        /// <inheritdoc />
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            throw new NotImplementedException("Method call translation is not implemented yet.");
            return base.VisitMethodCall(node);
        }

    }

}
