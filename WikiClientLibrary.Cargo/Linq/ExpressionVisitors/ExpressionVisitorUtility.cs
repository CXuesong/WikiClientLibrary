using System.Linq.Expressions;

namespace WikiClientLibrary.Cargo.Linq.ExpressionVisitors;

internal static class ExpressionVisitorUtility
{

    public static LambdaExpression UnwindLambdaExpression(Expression lambdaOrQuote)
    {
        return lambdaOrQuote switch
        {
            null => throw new ArgumentNullException(nameof(lambdaOrQuote)),
            LambdaExpression lambda => lambda,
            UnaryExpression unary when unary.Operand is LambdaExpression lambda1 && unary.NodeType == ExpressionType.Quote => lambda1,
            _ => throw new ArgumentException($"Provided expression cannot be unwound to LambdaExpression: {lambdaOrQuote}.",
                nameof(lambdaOrQuote)),
        };
    }

}
