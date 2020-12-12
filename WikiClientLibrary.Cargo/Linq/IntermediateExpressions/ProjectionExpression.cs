using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace WikiClientLibrary.Cargo.Linq.IntermediateExpressions
{

    /// <summary>
    /// Projection of a SQL expression into a specific alias (<c><see cref="Expression"/> AS <see cref="Alias"/></c>).
    /// </summary>
    internal class ProjectionExpression : CargoSqlExpression
    {

        public ProjectionExpression(Expression expression, string alias)
        {
            Expression = expression;
            Alias = alias;
        }

        /// <inheritdoc />
        public override Type Type => typeof(void);

        public Expression Expression { get; }

        public string Alias { get; }

    }

}
