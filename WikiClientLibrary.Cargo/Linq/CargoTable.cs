using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace WikiClientLibrary.Cargo.Linq
{

    public interface ICargoTable<T> : IQueryable<T>
    {
        string Name { get; }
    }

    internal class CargoTable<T> : ICargoTable<T>
    {

        public CargoTable(string name, CargoQueryProvider provider)
        {
            Debug.Assert(!string.IsNullOrEmpty(name));
            Debug.Assert(provider != null);
            Name = name;
            Expression = new CargoQueryRootExpression(name, typeof(IQueryable<T>));
            // Expression = Expression.Parameter(typeof(IQueryable<T>), "PLACEHOLDER");
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

        /// <inheritdoc />
        public IQueryProvider Provider { get; }

        /// <inheritdoc />
        public string Name { get; }

    }

}
