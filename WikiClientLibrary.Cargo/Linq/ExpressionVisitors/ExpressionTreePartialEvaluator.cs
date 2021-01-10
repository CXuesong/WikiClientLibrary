using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace WikiClientLibrary.Cargo.Linq.ExpressionVisitors
{

    /// <summary>
    /// Partially evaluates all the evaluable expressions in the expression tree.
    /// </summary>
    public class ExpressionTreePartialEvaluator : ExpressionVisitor
    {

        private readonly Stack<Expression> _exprStack = new Stack<Expression>();
        private readonly HashSet<Expression> _nonEvaluableExpressions = new HashSet<Expression>();

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

        private bool IsNonEvaluable(Expression expr)
        {
            if (IsWellKnownNonEvaluable(expr)) return true;
            return _nonEvaluableExpressions.Contains(expr);
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

        private void DeclareNonEvaluable(Expression expr)
        {
            Debug.Assert(!IsWellKnownNonEvaluable(expr), "No need to record well-known non-evaluable expressions.");
            _nonEvaluableExpressions.Add(expr);
        }

        /// <inheritdoc />
        public override Expression Visit(Expression node)
        {
            if (node == null)
                return null;
            Debug.WriteLine("Visit {0}：{1}", node.NodeType, node);
            _exprStack.Push(node);
            Expression result;
            try
            {
                result = base.Visit(node);
            }
            finally
            {
                var popped = _exprStack.Pop();
                Debug.Assert(popped == node);
            }
            if (IsWellKnownNonEvaluable(result) || _nonEvaluableExpressions.Contains(result))
            {
                // By default any expression containing non-evaluable expression is not evaluable.
                // Traverse through parents
                foreach (var parentExpr in _exprStack)
                {
                    if (IsNonEvaluable(parentExpr))
                    {
                        // If any expression in the ancestor path has been decided as non-evaluable, its parent must has been declared so.
                        break;
                    }
                    DeclareNonEvaluable(parentExpr);
                }
            }
            return result;
        }

        /// <inheritdoc />
        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            // Assume we can evaluate everything in the lambda first.
            var result = base.VisitLambda(node);
            // Do full evaluate expression at lambda body, if possible.
            if (!_nonEvaluableExpressions.Contains(node.Body))
                result = node.Update(Evaluate(node.Body), node.Parameters);
            return result;
        }

        /// <inheritdoc />
        protected override Expression VisitBinary(BinaryExpression node)
        {
            var left = Visit(node.Left);
            var right = Visit(node.Right);
            var isLeftEvaluable = !_nonEvaluableExpressions.Contains(left);
            var isRightEvaluable = !_nonEvaluableExpressions.Contains(right);
            // Full evaluable. Let caller check whether we can reduce this expression with others.
            if (isLeftEvaluable && isRightEvaluable)
            {
                return node;
            }
            // Not evaluable. Partially evaluate the lhs / rhs.
            if (isLeftEvaluable)
                left = Evaluate(left);
            if (isRightEvaluable)
                right = Evaluate(right);
            var result = node.Update(left, node.Conversion, right);
            DeclareNonEvaluable(result);
            return result;
        }

    }

}
