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
            if (expression == null) throw new ArgumentNullException(nameof(expression));
            if (alias == null) throw new ArgumentNullException(nameof(alias));
            if (alias.Length == 0) throw new ArgumentException("Parameter should not be empty.", nameof(alias));
            Expression = expression;
            Alias = alias;
        }

        /// <inheritdoc />
        public override Type Type => typeof(void);

        public Expression Expression { get; }

        public string Alias { get; }

        /// <inheritdoc />
        public override string ToString() => $"{Expression} AS {Alias}";

    }

}
