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

    /// <summary>
    /// Simplifies the LINQ expression into a single <see cref="CargoQueryExpression"/> root node.
    /// </summary>
    public class CargoQueryExpressionReducer : ExpressionVisitor
    {

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
                return node.Method.Name switch
                {
                    nameof(Queryable.Select) => ProcessSelectCall((CargoQueryExpression)Visit(node.Arguments[0]),
                        UnwindLambdaExpression(Visit(node.Arguments[1]))),
                    nameof(Queryable.Where) => ProcessWhereCall((CargoQueryExpression)Visit(node.Arguments[0]),
                        UnwindLambdaExpression(Visit(node.Arguments[1]))),
                    nameof(Queryable.Take) => ProcessTakeCall((CargoQueryExpression)Visit(node.Arguments[0]), (ConstantExpression)Visit(node.Arguments[1])),
                    nameof(Queryable.Skip) => ProcessSkipCall((CargoQueryExpression)Visit(node.Arguments[0]), (ConstantExpression)Visit(node.Arguments[1])),
                    nameof(Queryable.OrderBy) => ProcessOrderByCall((CargoQueryExpression)Visit(node.Arguments[0]),
                        UnwindLambdaExpression(Visit(node.Arguments[1])), false),
                    nameof(Queryable.ThenBy) => ProcessThenOrderByCall((CargoQueryExpression)Visit(node.Arguments[0]),
                        UnwindLambdaExpression(Visit(node.Arguments[1])), false),
                    nameof(Queryable.OrderByDescending) => ProcessOrderByCall((CargoQueryExpression)Visit(node.Arguments[0]),
                        UnwindLambdaExpression(Visit(node.Arguments[1])), true),
                    nameof(Queryable.ThenByDescending) => ProcessThenOrderByCall((CargoQueryExpression)Visit(node.Arguments[0]),
                        UnwindLambdaExpression(Visit(node.Arguments[1])), true),
                    _ => throw new NotSupportedException($"Queryable method call is not supported: {node.Method}.")
                };
            }
            return node;
        }

        private MemberAccessExpressionReplacer CreateModelMemberAccessReplacer(ParameterExpression target, IEnumerable<ProjectionExpression> knownProjections)
        {
            return new MemberAccessExpressionReplacer(target, 
                knownProjections.ToDictionary(f => f.TargetMember, f => f.Expression));
        }

        private CargoQueryExpression ProcessSelectCall(CargoQueryExpression source, LambdaExpression selector)
        {
            Debug.Assert(selector.Parameters.Count == 1);
            if (selector.Body == source)
                return source;

            // TODO table joins
            var fieldRefReplacer = CreateModelMemberAccessReplacer(selector.Parameters[0], source.Fields);
            // Process `new { X = item.A, Y = item.B }`
            if (selector.Body is NewExpression newExpr)
            {
                Debug.Assert(newExpr.Members != null);
                Debug.Assert(newExpr.Members.Count == newExpr.Arguments.Count);
                var fields = newExpr.Members.Zip(newExpr.Arguments, (m, a) =>
                    new ProjectionExpression(fieldRefReplacer.VisitAndConvert(a, "Select"), ProjectionExpression.MangleAlias(m.Name), m)
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
            
            var fieldRefReplacer = CreateModelMemberAccessReplacer(predicate.Parameters[0], source.Fields);
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
            var fieldRefReplacer = CreateModelMemberAccessReplacer(keySelector.Parameters[0], source.Fields);
            return source.SetOrderBy(fieldRefReplacer.VisitAndConvert(keySelector.Body, "OrderBy"), descending);
        }

        private CargoQueryExpression ProcessThenOrderByCall(CargoQueryExpression source, LambdaExpression keySelector, bool descending)
        {
            var fieldRefReplacer = CreateModelMemberAccessReplacer(keySelector.Parameters[0], source.Fields);
            return source.SetOrderBy(fieldRefReplacer.VisitAndConvert(keySelector.Body, "ThenOrderBy"), descending);
        }

    }

}
