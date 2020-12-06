using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace WikiClientLibrary.Cargo.Linq.ExpressionVisitors
{

    public class CargoQueryParametersBuilder : ExpressionVisitor
    {

        private List<string> fields = new List<string>();
        private List<string> tables = new List<string>();
        private List<string> whereClauses = new List<string>();

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
                        return ProcessSelectCall(node);
                    case nameof(Queryable.Where):
                        return ProcessWhereCall(node);
                }
            }
            return base.VisitMethodCall(node);
        }

        protected virtual Expression ProcessSelectCall(MethodCallExpression node)
        {
            throw new NotImplementedException();
        }

        protected virtual Expression ProcessWhereCall(MethodCallExpression node)
        {
            throw new NotImplementedException();
        }

    }

}
