using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace WikiClientLibrary.Cargo.Linq.ExpressionVisitors
{

    internal static class ExpressionVisitorUtility
    {

        public static LambdaExpression UnwindLambdaExpression(Expression lambdaOrQuote)
        {
            switch (lambdaOrQuote)
            {
                case null:
                    throw new ArgumentNullException(nameof(lambdaOrQuote));
                case LambdaExpression lambda:
                    return lambda;
                case UnaryExpression unary when unary.Operand is LambdaExpression lambda1 && unary.NodeType == ExpressionType.Quote:
                    return lambda1;
                default:
                    throw new ArgumentException($"Provided expression cannot be unwound to LambdaExpression: {lambdaOrQuote}.", nameof(lambdaOrQuote));
            }
        }

    }

}
