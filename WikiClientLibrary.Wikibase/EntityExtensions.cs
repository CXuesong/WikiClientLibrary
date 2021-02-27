using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WikiClientLibrary.Wikibase
{
    public static class EntityExtensions
    {

        /// <inheritdoc cref="RefreshAsync(IEnumerable{Entity},EntityQueryOptions,ICollection{string},CancellationToken)"/>
        public static Task RefreshAsync(this IEnumerable<Entity> entities)
        {
            return RefreshAsync(entities, EntityQueryOptions.None, null, CancellationToken.None);
        }

        /// <inheritdoc cref="RefreshAsync(IEnumerable{Entity},EntityQueryOptions,ICollection{string},CancellationToken)"/>
        public static Task RefreshAsync(this IEnumerable<Entity> entities, EntityQueryOptions options)
        {
            return RefreshAsync(entities, options, null, CancellationToken.None);
        }

        /// <inheritdoc cref="RefreshAsync(IEnumerable{Entity},EntityQueryOptions,ICollection{string},CancellationToken)"/>
        public static Task RefreshAsync(this IEnumerable<Entity> entities, EntityQueryOptions options, ICollection<string> languages)
        {
            return RefreshAsync(entities, options, languages, CancellationToken.None);
        }

        /// <summary>
        /// Asynchronously fetch information for a sequence of entities.
        /// </summary>
        /// <param name="entities">A sequence of entities to be refreshed.</param>
        /// <param name="options">Provides options when performing the query.</param>
        /// <param name="languages">
        /// Filter down the internationalized values to the specified one or more language codes.
        /// Set to <c>null</c> to fetch for all available languages.
        /// </param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <seealso cref="Entity.RefreshAsync()"/>
        public static Task RefreshAsync(this IEnumerable<Entity> entities, EntityQueryOptions options,
            ICollection<string>? languages, CancellationToken cancellationToken)
        {
            return WikibaseRequestHelper.RefreshEntitiesAsync(entities, options, languages, cancellationToken);
        }

    }
}
