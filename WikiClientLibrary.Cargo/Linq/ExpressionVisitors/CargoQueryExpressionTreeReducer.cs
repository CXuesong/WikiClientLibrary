using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace WikiClientLibrary.Cargo.Linq.ExpressionVisitors
{
    public class CargoQueryExpressionTreeReducer : ExpressionVisitor
    {
        /// <inheritdoc />
        public override Expression Visit(Expression node)
        {
            var visitedNode = base.Visit(node);
            // Simplify constant values from closure.
            if (visitedNode is MemberExpression memberExp)
            {
                if (memberExp.Expression is ConstantExpression constant)
                {
                    switch (memberExp.Member)
                    {
                        case FieldInfo field:
                            return Expression.Constant(field.GetValue(constant.Value), field.FieldType);
                        case PropertyInfo property:
                            return Expression.Constant(property.GetValue(constant.Value), property.PropertyType);
                    }
                }
            }
            return visitedNode;
        }

    }
}
