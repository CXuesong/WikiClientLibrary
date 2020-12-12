using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using WikiClientLibrary.Cargo.Linq.IntermediateExpressions;

namespace WikiClientLibrary.Cargo.Linq
{

    /// <summary>
    /// A queryable Cargo table instance.
    /// </summary>
    /// <typeparam name="T">type of the model.</typeparam>
    public interface ICargoTable<T> : IQueryable<T>
    {
        string Name { get; }
    }

    internal class CargoTable<T> : ICargoTable<T>
    {

        public CargoTable(CargoModel model, CargoQueryProvider provider)
        {
            Debug.Assert(model != null);
            Debug.Assert(provider != null);
            Expression = new CargoQueryExpression(model, typeof(T));
            Model = model;
            Provider = provider;
        }

        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc />
        public Type ElementType => typeof(T);

        /// <inheritdoc />
        public Expression Expression { get; }

        public CargoModel Model { get; }

        /// <inheritdoc />
        public IQueryProvider Provider { get; }

        /// <inheritdoc />
        public string Name => Model.Name;

    }

}
