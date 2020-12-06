using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Cargo.Linq
{

    public class CargoQueryProvider: IQueryProvider
    {

        public CargoQueryProvider(WikiSite wikiSite) 
            : this(wikiSite, 10)
        {
        }

        public CargoQueryProvider(WikiSite wikiSite, int paginationSize)
        {
            if (paginationSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(paginationSize));
            PaginationSize = paginationSize;
            WikiSite = wikiSite ?? throw new ArgumentNullException(nameof(wikiSite));
        }

        public WikiSite WikiSite { get; }

        public int PaginationSize { get; }

        /// <inheritdoc />
        public IQueryable CreateQuery(Expression expression)
        {
            var queryableType = expression.Type.GetTypeInfo().ImplementedInterfaces
                .First(i => i.IsConstructedGenericType && i.GetGenericTypeDefinition() == typeof(IQueryable<>));
            var elementType = queryableType.GenericTypeArguments[0];
            return (IQueryable)Activator.CreateInstance(typeof(CargoQuery<>).MakeGenericType(elementType), this, expression);
        }

        /// <inheritdoc />
        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new CargoQuery<TElement>(this, expression);
        }

        /// <inheritdoc />
        public object Execute(Expression expression)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public TResult Execute<TResult>(Expression expression)
        {
            throw new NotImplementedException();
        }

    }
}
