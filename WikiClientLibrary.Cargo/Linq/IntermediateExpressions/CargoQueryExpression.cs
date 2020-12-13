using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace WikiClientLibrary.Cargo.Linq.IntermediateExpressions
{

    /// <summary>
    /// Represents a full <c>SELECT</c> query expression that can be later converted into Cargo query parameters.
    /// </summary>
    internal class CargoQueryExpression : CargoSqlExpression
    {

        public CargoQueryExpression()
        {
        }

        public CargoQueryExpression(CargoModel model, Type recordType)
        {
            const string tableAlias = "T0";
            RecordType = recordType;
            Fields = model.Properties.Select(p =>
                new ProjectionExpression(new FieldRefExpression(tableAlias, p), p.Name)
            ).ToImmutableList();
            Tables = ImmutableList.Create(new TableProjectionExpression(model, tableAlias));
            ClrMemberMapping = Fields.ToDictionary(f => f.Alias, f => f.Expression);
        }

        /// <inheritdoc />
        /// <remarks>This property returns <see cref="IQueryable{T}"/> of <see cref="RecordType"/>.</remarks>
        public override Type Type => typeof(IQueryable<>).MakeGenericType(RecordType);

        /// <summary>Type of the projected record set.</summary>
        public Type RecordType { get; private set; }

        /// <summary>Field projections (<c>SELECT ...</c>).</summary>
        public IImmutableList<ProjectionExpression> Fields { get; private set; } = ImmutableList<ProjectionExpression>.Empty;

        /// <summary>Filter condition (<c>WHERE ...</c>).</summary>
        public Expression Predicate { get; private set; } = null;

        /// <summary>Sort condition.</summary>
        public IImmutableList<OrderByExpression> OrderBy { get; private set; } = ImmutableList<OrderByExpression>.Empty;

        /// <summary>Table and alias.</summary>
        public IImmutableList<TableProjectionExpression> Tables { get; private set; } = ImmutableList<TableProjectionExpression>.Empty;

        public int Offset { get; private set; }

        public int? Limit { get; private set; }

        public IReadOnlyDictionary<string, Expression> ClrMemberMapping { get; private set; }

        public CargoQueryExpression Project(IImmutableList<ProjectionExpression> fields, Type recordType)
        {
            Debug.Assert(fields != null);
            Debug.Assert(recordType != null);
            var newInst = (CargoQueryExpression)MemberwiseClone();
            newInst.RecordType = recordType;
            newInst.Fields = fields;
            newInst.ClrMemberMapping = fields.ToDictionary(f => f.Alias, f => f.Expression);
            return newInst;
        }

        public CargoQueryExpression Filter(Expression predicate)
        {
            return SetPredicate(Predicate == null ? predicate : AndAlso(Predicate, predicate));
        }

        public CargoQueryExpression SetPredicate(Expression predicate)
        {
            Debug.Assert(predicate != null);
            var newInst = (CargoQueryExpression)MemberwiseClone();
            newInst.Predicate = predicate;
            return newInst;
        }

        public CargoQueryExpression SetOffset(int offset)
        {
            Debug.Assert(offset >= 0);
            var newInst = (CargoQueryExpression)MemberwiseClone();
            newInst.Offset = offset;
            return newInst;
        }

        public CargoQueryExpression SetLimit(int? limit)
        {
            Debug.Assert(limit == null || limit >= 0);
            var newInst = (CargoQueryExpression)MemberwiseClone();
            newInst.Limit = limit;
            return newInst;
        }

        public CargoQueryExpression Skip(int offset)
        {
            Debug.Assert(offset >= 0);
            if (offset == 0 || Limit == 0) return this;
            var newInst = (CargoQueryExpression)MemberwiseClone();
            newInst.Offset = Offset + offset;
            if (Limit != null)
            {
                // Need to re-evaluate limit
                newInst.Limit = Math.Max(0, Limit.Value - offset);
            }
            return newInst;
        }

        public CargoQueryExpression SetOrderBy(Expression expression, bool descending = false)
        {
            Debug.Assert(expression != null);
            var newInst = (CargoQueryExpression)MemberwiseClone();
            newInst.OrderBy = ImmutableList.Create(new OrderByExpression(expression, descending));
            return newInst;
        }

        public CargoQueryExpression ThenOrderBy(Expression expression, bool descending = false)
        {
            Debug.Assert(expression != null);
            var newInst = (CargoQueryExpression)MemberwiseClone();
            newInst.OrderBy = OrderBy.Add(new OrderByExpression(expression, descending));
            return newInst;
        }

    }
}
