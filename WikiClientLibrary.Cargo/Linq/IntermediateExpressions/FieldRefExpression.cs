using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using WikiClientLibrary.Cargo.Schema;

namespace WikiClientLibrary.Cargo.Linq.IntermediateExpressions
{
    /// <summary>
    /// Reference to a field (i.e. model property) in a Cargo table.
    /// </summary>
    internal class FieldRefExpression : CargoSqlExpression
    {

        public FieldRefExpression(string tableAlias, CargoModelProperty property)
        {
            Debug.Assert(!string.IsNullOrEmpty(tableAlias));
            Debug.Assert(property != null);
            TableAlias = tableAlias;
            Property = property;
        }

        /// <inheritdoc />
        public override Type Type => Property.ClrType;

        /// <summary>Table name or its alias.</summary>
        public string TableAlias { get; }

        /// <summary>Model metadata.</summary>
        public new CargoModelProperty Property { get; }

        public string FieldName => Property.Name;

        /// <inheritdoc />
        public override string ToString() => $"{TableAlias}.{FieldName}";

    }
}
