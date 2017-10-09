using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WikiClientLibrary.Wikibase
{
    public static class WbEntityExtensions
    {
        public static Task RefreshAsync(this IEnumerable<WbEntity> entities)
        {
            return RefreshAsync(entities, WbEntityQueryOptions.None, null, CancellationToken.None);
        }

        public static Task RefreshAsync(this IEnumerable<WbEntity> entities, WbEntityQueryOptions options)
        {
            return RefreshAsync(entities, options, null, CancellationToken.None);
        }

        public static Task RefreshAsync(this IEnumerable<WbEntity> entities, WbEntityQueryOptions options, ICollection<string> languages)
        {
            return RefreshAsync(entities, options, languages, CancellationToken.None);
        }

        public static Task RefreshAsync(this IEnumerable<WbEntity> entities, WbEntityQueryOptions options,
            ICollection<string> languages, CancellationToken cancellationToken)
        {
            return WikibaseRequestHelper.RefreshEntitiesAsync(entities, options, languages, cancellationToken);
        }

    }
}
