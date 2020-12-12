using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Cargo.Linq
{

    /// <summary>
    /// Provides LINQ to Cargo query ability.
    /// </summary>
    public interface ICargoQueryContext
    {

        /// <summary>
        /// Starts a Linq expression on the specified table.
        /// </summary>
        /// <typeparam name="T">type of the model.</typeparam>
        /// <param name="name">name of the Cargo table. Specify <c>null</c> to use default table name corresponding to the model.</param>
        /// <returns>LINQ root.</returns>
        IQueryable<T> Table<T>(string name = null);

    }

    public class CargoQueryContext : ICargoQueryContext
    {

        public CargoQueryContext(WikiSite wikiSite)
        {
            WikiSite = wikiSite ?? throw new ArgumentNullException(nameof(wikiSite));
        }

        public WikiSite WikiSite { get; }

        /// <inheritdoc />
        public IQueryable<T> Table<T>() => Table<T>(null);

        /// <inheritdoc />
        public IQueryable<T> Table<T>(string name)
        {
            return new CargoTable<T>(CargoModel.FromClrType(typeof(T), name),
                new CargoQueryProvider(WikiSite));
        }

    }

}
