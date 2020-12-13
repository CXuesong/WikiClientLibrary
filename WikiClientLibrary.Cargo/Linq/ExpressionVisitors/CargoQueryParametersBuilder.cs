using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using WikiClientLibrary.Cargo.Linq.IntermediateExpressions;
using static WikiClientLibrary.Cargo.Linq.ExpressionVisitors.ExpressionVisitorUtility;

namespace WikiClientLibrary.Cargo.Linq.ExpressionVisitors
{

    public class CargoQueryParametersBuilder : ExpressionVisitor
    {

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

        /// <inheritdoc />
        public override Expression Visit(Expression node)
        {
            Debug.WriteLine(node == null ? "<null>" : (node.GetType().Name + ":" + node));
            Debug.WriteLine("");
            return base.Visit(node);
        }

        /// <inheritdoc />
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // c.f. https://github.com/dotnet/efcore/blob/main/src/EFCore/Query/QueryableMethodTranslatingExpressionVisitor.cs
            // c.f. https://github.com/dotnet/efcore/blob/main/src/EFCore.InMemory/Query/Internal/InMemoryQueryableMethodTranslatingExpressionVisitor.cs
            if (node.Method.DeclaringType == typeof(Queryable))
            {
                switch (node.Method.Name)
                {
                    case nameof(Queryable.Select):
                        return ProcessSelectCall((CargoQueryExpression)Visit(node.Arguments[0]), UnwindLambdaExpression(Visit(node.Arguments[1])));
                    case nameof(Queryable.Where):
                        return ProcessWhereCall((CargoQueryExpression)Visit(node.Arguments[0]), UnwindLambdaExpression(Visit(node.Arguments[1])));
                    case nameof(Queryable.Take):
                        return ProcessTakeCall((CargoQueryExpression)Visit(node.Arguments[0]), (ConstantExpression)Visit(node.Arguments[1]));
                    case nameof(Queryable.Skip):
                        return ProcessSkipCall((CargoQueryExpression)Visit(node.Arguments[0]), (ConstantExpression)Visit(node.Arguments[1]));
                    case nameof(Queryable.OrderBy):
                        return ProcessOrderByCall((CargoQueryExpression)Visit(node.Arguments[0]), UnwindLambdaExpression(Visit(node.Arguments[1])), false);
                    case nameof(Queryable.ThenBy):
                        return ProcessThenOrderByCall((CargoQueryExpression)Visit(node.Arguments[0]), UnwindLambdaExpression(Visit(node.Arguments[1])), false);
                    case nameof(Queryable.OrderByDescending):
                        return ProcessOrderByCall((CargoQueryExpression)Visit(node.Arguments[0]), UnwindLambdaExpression(Visit(node.Arguments[1])), true);
                    case nameof(Queryable.ThenByDescending):
                        return ProcessThenOrderByCall((CargoQueryExpression)Visit(node.Arguments[0]), UnwindLambdaExpression(Visit(node.Arguments[1])), true);
                }
            }
            throw new NotSupportedException("Not supported method call.");
        }

        private CargoQueryExpression ProcessSelectCall(CargoQueryExpression source, LambdaExpression selector)
        {
            Debug.Assert(selector.Parameters.Count == 1);
            var pItem = selector.Parameters[0];
            if (selector.Body == source)
                return source;

            // TODO table joins
            var fieldRefReplacer = new MemberAccessExpressionReplacer(pItem, source.ClrMemberMapping);
            // Process `new { X = item.A, Y = item.B }`
            if (selector.Body is NewExpression newExpr)
            {
                Debug.Assert(newExpr.Members != null);
                Debug.Assert(newExpr.Members.Count == newExpr.Arguments.Count);
                var fields = newExpr.Members.Zip(newExpr.Arguments, (m, a) =>
                    new ProjectionExpression(fieldRefReplacer.VisitAndConvert(a, "Select"), m.Name)
                ).ToImmutableList();
                return source.Project(fields, selector.ReturnType);
            }

            throw new NotSupportedException("Not supported Select expression.");
        }

        private CargoQueryExpression ProcessWhereCall(CargoQueryExpression source, LambdaExpression predicate)
        {
            Debug.Assert(predicate.Parameters.Count == 1);

            if (predicate.Body is ConstantExpression predicateBody)
            {
                var predicateVal = (bool)predicateBody.Value;
                return predicateVal ? source : source.SetPredicate(predicateBody);
            }

            if (source.Limit != null || source.Offset != 0)
            {
                throw new NotSupportedException(".Where after .Take or .Skip is not translatable.");
            }

            var pItem = predicate.Parameters[0];
            var fieldRefReplacer = new MemberAccessExpressionReplacer(pItem, source.ClrMemberMapping);
            return source.Filter(fieldRefReplacer.VisitAndConvert(predicate.Body, "Where"));
        }

        private CargoQueryExpression ProcessTakeCall(CargoQueryExpression source, ConstantExpression count)
        {
            var countVal = (int)count.Value;
            if (source.Limit == null || source.Limit > countVal)
                return source.SetLimit(countVal);
            return source;
        }

        private CargoQueryExpression ProcessSkipCall(CargoQueryExpression source, ConstantExpression count)
        {
            var countVal = (int)count.Value;
            return source.Skip(countVal);
        }

        private CargoQueryExpression ProcessOrderByCall(CargoQueryExpression source, LambdaExpression keySelector, bool descending)
        {
            var pItem = keySelector.Parameters[0];
            var fieldRefReplacer = new MemberAccessExpressionReplacer(pItem, source.ClrMemberMapping);
            return source.SetOrderBy(fieldRefReplacer.VisitAndConvert(keySelector.Body, "OrderBy"), descending);
        }

        private CargoQueryExpression ProcessThenOrderByCall(CargoQueryExpression source, LambdaExpression keySelector, bool descending)
        {
            var pItem = keySelector.Parameters[0];
            var fieldRefReplacer = new MemberAccessExpressionReplacer(pItem, source.ClrMemberMapping);
            return source.SetOrderBy(fieldRefReplacer.VisitAndConvert(keySelector.Body, "ThenOrderBy"), descending);
        }

    }

}
