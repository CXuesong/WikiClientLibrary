using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using WikiClientLibrary.Cargo.Linq.IntermediateExpressions;

namespace WikiClientLibrary.Cargo.Linq.ExpressionVisitors
{

    /// <summary>
    /// Partially evaluates all the evaluable expressions in the expression tree.
    /// </summary>
    public class ExpressionTreePartialEvaluator : ExpressionVisitor
    {

        private readonly Stack<TraversalStateFrame> _traversalStack = new Stack<TraversalStateFrame>();

        private TraversalStateFrame CurrentStateFrame => _traversalStack.Peek();

        private readonly HashSet<Expression> _nonEvaluableExpressions = new HashSet<Expression>(ExpressionEqualityComparer.Default)
        {
            Expression.MakeMemberAccess(null, typeof(DateTime).GetRuntimeProperty(nameof(DateTime.Now))),
            Expression.MakeMemberAccess(null, typeof(DateTimeOffset).GetRuntimeProperty(nameof(DateTimeOffset.Now))),
        };

        public ExpressionTreePartialEvaluator()
        {
            // Dummy root state. `Visit` needs this.
            _traversalStack.Push(new TraversalStateFrame(null, false));
        }

        private ConstantExpression Evaluate(Expression expr)
        {
            try
            {
                var value = Expression.Lambda<Func<object>>(Expression.Convert(expr, typeof(object))).Compile()();
                return Expression.Constant(value, expr.Type);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to partial evaluate the expression.", ex);
            }
        }

        private bool IsWellKnownNonEvaluable(Expression expr)
        {
            Debug.Assert(expr != null);
            if (expr.NodeType == ExpressionType.Parameter || expr.NodeType == ExpressionType.Extension)
            {
                return true;
            }
            return false;
        }

        /// <inheritdoc />
        public override Expression Visit(Expression node)
        {
            if (node == null)
                return null;
            // Debug.WriteLine("Visit {0}：{1}", node.NodeType, node);
            var state = new TraversalStateFrame(node, !IsWellKnownNonEvaluable(node));
            _traversalStack.Push(state);
            Expression result;
            try
            {
                result = base.Visit(node);
            }
            finally
            {
                var popped = _traversalStack.Pop();
                Debug.Assert(popped == state);
            }
            // Tell parent `node` is not evaluable.
            var nodeEvaluable = state.IsEvaulable && !_nonEvaluableExpressions.Contains(result);
            CurrentStateFrame.LastVisitEvaluable = nodeEvaluable;
            // Also set parent as non-evaluable automatically. Parent can reset this flag later if necessary.
            if (!nodeEvaluable)
                CurrentStateFrame.IsEvaulable = false;
            return result;
        }

        /// <inheritdoc />
        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            // Assume we can evaluate everything in the lambda first.
            var body = Visit(node.Body);
            // Do full evaluate expression at lambda body, if possible.
            if (CurrentStateFrame.LastVisitEvaluable)
                body = Evaluate(body);
            node = node.Update(body, node.Parameters);
            return node;
        }

        /// <inheritdoc />
        protected override Expression VisitBinary(BinaryExpression node)
        {
            var left = Visit(node.Left);
            var leftEvaluable = CurrentStateFrame.LastVisitEvaluable;
            var right = Visit(node.Right);
            var rightEvaluable = CurrentStateFrame.LastVisitEvaluable;
            // Full evaluable. Let caller check whether we can reduce this expression with others.
            if (leftEvaluable && rightEvaluable)
                return node;
            // Not evaluable. Partially evaluate the lhs / rhs.
            if (leftEvaluable)
                left = Evaluate(left);
            if (rightEvaluable)
                right = Evaluate(right);
            var result = node.Update(left, node.Conversion, right);
            return result;
        }

        /// <inheritdoc />
        protected override Expression VisitExtension(Expression node)
        {
            switch (node)
            {
                case CargoBinaryOperationExpression bin:
                {
                    var left = Visit(bin.Left);
                    if (CurrentStateFrame.LastVisitEvaluable)
                        left = Evaluate(left);
                    var right = Visit(bin.Right);
                    if (CurrentStateFrame.LastVisitEvaluable)
                        right = Evaluate(right);
                    return bin.Update(left, right);
                }
                case CargoFunctionExpression func:
                {
                    var argBuilder = func.Arguments.ToImmutableList().ToBuilder();
                    for (int i = 0; i < func.Arguments.Count; i++)
                    {
                        var a = Visit(func.Arguments[i]);
                        if (CurrentStateFrame.LastVisitEvaluable)
                            a = Evaluate(a);
                        if (a != func.Arguments[i])
                            argBuilder[i] = a;
                    }
                    return func.Update(argBuilder.ToImmutable());
                }
            }
            return base.VisitExtension(node);
        }

        [DebuggerDisplay("{OriginalExpression}")]
        private sealed class TraversalStateFrame
        {
            // For debugging purpose.
            public readonly Expression OriginalExpression;

            public bool IsEvaulable;

            public bool LastVisitEvaluable;

            public TraversalStateFrame(Expression originalExpression, bool isEvaulable)
            {
                OriginalExpression = originalExpression;
                IsEvaulable = isEvaulable;
            }
        }

    }

}
