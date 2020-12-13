using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace WikiClientLibrary.Cargo.Linq.ExpressionVisitors
{

    /// <summary>
    /// Partially evaluates all the evaluatable expressions in the expression tree.
    /// </summary>
    public class ExpressionTreePartialEvaluator : ExpressionVisitor
    {

        private bool _isReducible;

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

        /// <inheritdoc />
        protected override Expression VisitBinary(BinaryExpression node)
        {
            _isReducible = true;
            var left = Visit(node.Left);
            var isLeftReducible = _isReducible;
            _isReducible = true;
            var right = Visit(node.Right);
            var isRightReducible = _isReducible;
            if (isLeftReducible && isRightReducible)
                return Evaluate(node);
            if (isLeftReducible)
                left = Evaluate(left);
            if (isRightReducible)
                right = Evaluate(right);
            return node.Update(left, node.Conversion, right);
        }

        /// <inheritdoc />
        protected override Expression VisitParameter(ParameterExpression node)
        {
            _isReducible = false;
            return base.VisitParameter(node);
        }

        /// <inheritdoc />
        protected override Expression VisitExtension(Expression node)
        {
            _isReducible = false;
            return node;
        }

    }

}
