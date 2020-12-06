using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Cargo.Linq
{
    public interface ICargoQueryContext
    {

        IQueryable<T> Table<T>(string name);

    }

    public class CargoQueryContext : ICargoQueryContext
    {

        public CargoQueryContext(WikiSite wikiSite)
        {
            WikiSite = wikiSite ?? throw new ArgumentNullException(nameof(wikiSite));
        }

        public WikiSite WikiSite { get; }

        /// <inheritdoc />
        public IQueryable<T> Table<T>(string name)
        {
            return new CargoTable<T>(name, new CargoQueryProvider(WikiSite));
        }

    }

}
