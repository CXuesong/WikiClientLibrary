using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;

namespace WikiClientLibrary.Cargo.Linq
{
    internal class CargoQueryRootExpression : Expression
    {

        public CargoQueryRootExpression(string tableName, Type clrType)
        {
            Debug.Assert(!string.IsNullOrEmpty(tableName));
            Debug.Assert(clrType != null);
            TableName = tableName;
            Type = clrType;
        }

        /// <inheritdoc />
        public override Type Type { get; }

        /// <summary>Cargo table name.</summary>
        public string TableName { get; }

        /// <inheritdoc />
        public override bool CanReduce => false;

        /// <inheritdoc />
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <inheritdoc />
        protected override Expression VisitChildren(ExpressionVisitor visitor) => this;

        /// <inheritdoc />
        public override bool Equals(object obj)
            => obj != null && (ReferenceEquals(obj, this) || obj is CargoQueryRootExpression exp && exp.Type == Type && exp.TableName == TableName);

        /// <inheritdoc />
        public override int GetHashCode() => Type.GetHashCode() ^ TableName.GetHashCode();

    }
}
