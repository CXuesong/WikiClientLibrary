using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace WikiClientLibrary.Cargo.Linq.IntermediateExpressions
{

    /// <summary>
    /// Projection of a SQL expression into a specific alias (<c><see cref="Expression"/> AS <see cref="Alias"/></c>).
    /// </summary>
    internal class ProjectionExpression : CargoSqlExpression
    {

        public ProjectionExpression(Expression expression, string alias, MemberInfo targetMember)
        {
            if (expression == null) throw new ArgumentNullException(nameof(expression));
            if (alias == null) throw new ArgumentNullException(nameof(alias));
            if (alias.Length == 0) throw new ArgumentException("Parameter should not be empty.", nameof(alias));
            if (targetMember == null) throw new ArgumentNullException(nameof(targetMember));
            Expression = expression;
            Alias = alias;
            TargetMember = targetMember;
        }

        /// <inheritdoc />
        public override Type Type => typeof(void);

        /// <summary>The field access or other complex evaluation expression to be aliased.</summary>
        public Expression Expression { get; }

        /// <summary>The (mangled) alias of the SQL expression.</summary>
        public string Alias { get; }

        /// <summary>When populating the model object, this is the property / field to be populated.</summary>
        public MemberInfo TargetMember { get; }

        /// <inheritdoc />
        public override string ToString() => $"{Expression} AS {Alias}";

        public static string MangleAlias(string alias)
        {
            if (alias == null)
                throw new ArgumentNullException(nameof(alias));
            if (alias.Length == 0) return alias;
            // Aliases starting with underscore causes error with Cargo API.
            if (alias[0] == '_' || alias.StartsWith("wcl_", StringComparison.OrdinalIgnoreCase))
                return "wcl_p" + alias;
            return alias;
        }

        public static string UnmangleAlias(string alias)
        {
            if (alias == null)
                throw new ArgumentNullException(nameof(alias));
            if (alias.StartsWith("wcl_p"))
                return alias.Substring(5);
            return alias;
        }

    }

}
