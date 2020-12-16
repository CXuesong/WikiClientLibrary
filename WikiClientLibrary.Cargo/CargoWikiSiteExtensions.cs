using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Infrastructures.Logging;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Cargo
{

    /// <summary>
    /// Extension methods for MediaWiki sites with <a href="https://www.mediawiki.org/wiki/Special:MyLanguage/Extension:Cargo">Cargo extension</a> installed.
    /// </summary>
    public static class CargoWikiSiteExtensions
    {

        /// <inheritdoc cref="ExecuteCargoQueryAsync(WikiSite,CargoQueryParameters,CancellationToken)"/>
        public static Task<IList<JObject>> ExecuteCargoQueryAsync(this WikiSite site, CargoQueryParameters queryParameters)
        {
            return ExecuteCargoQueryAsync(site, queryParameters, default);
        }

        /// <summary>
        /// Executes a Cargo query and retrieves the response rows.
        /// </summary>
        /// <param name="site">a MediaWiki site with Cargo extension installed.</param>
        /// <param name="queryParameters">query parameters.</param>
        /// <param name="cancellationToken">a token used to cancel the operation.</param>
        /// <exception cref="ArgumentNullException"><paramref name="site"/> or <paramref name="queryParameters"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="queryParameters"/>.<see cref="CargoQueryParameters.Limit" /> is 0 or negative.
        /// - or -
        /// <paramref name="queryParameters"/>.<see cref="CargoQueryParameters.Offset" /> is negative.
        /// </exception>
        /// <exception cref="MediaWikiRemoteException">
        /// When query execution failed, Cargo usually fails with <c>internal_api_error_MWException</c> error.
        /// You should observe <c>"MWException"</c> as the value of <see cref="MediaWikiRemoteException.ErrorClass"/>.
        /// </exception>
        /// <returns>a list of rows, each item is a JSON object with field name as key.</returns>
        public static async Task<IList<JObject>> ExecuteCargoQueryAsync(this WikiSite site, CargoQueryParameters queryParameters, CancellationToken cancellationToken)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (queryParameters == null) throw new ArgumentNullException(nameof(queryParameters));
            if (queryParameters.Limit <= 0)
                throw new ArgumentOutOfRangeException(nameof(queryParameters) + "." + nameof(queryParameters.Limit));
            if (queryParameters.Offset < 0)
                throw new ArgumentOutOfRangeException(nameof(queryParameters) + "." + nameof(queryParameters.Offset));
            var tables = queryParameters.Tables != null ? string.Join(",", queryParameters.Tables) : null;
            if (string.IsNullOrEmpty(tables))
                throw new ArgumentException("queryParameters.Tables should not be null or empty.", nameof(queryParameters) + "." + nameof(queryParameters.Tables));
            var fields = queryParameters.Fields != null ? string.Join(",", queryParameters.Fields) : null;
            if (string.IsNullOrEmpty(fields))
                throw new ArgumentException("queryParameters.Fields should not be null or empty.", nameof(queryParameters) + "." + nameof(queryParameters.Fields));
            var join_on = queryParameters.JoinOn != null ? string.Join(",", queryParameters.JoinOn) : null;
            var order_by = queryParameters.OrderBy != null ? string.Join(",", queryParameters.OrderBy) : null;
            using (site.BeginActionScope(site, nameof(ExecuteCargoQueryAsync)))
            {
                if (site.Logger.IsEnabled(LogLevel.Debug))
                {
                    var sb = new StringBuilder(128);
                    sb.Append("SELECT ");
                    sb.Append(fields);
                    sb.Append(" FROM ");
                    sb.Append(tables);
                    sb.AppendLine();
                    if (queryParameters.Where != null)
                    {
                        sb.Append(" WHERE ");
                        sb.Append(queryParameters.Where);
                        sb.AppendLine();
                    }
                    if (queryParameters.GroupBy != null)
                    {
                        sb.Append(" GROUP BY ");
                        sb.Append(queryParameters.GroupBy);
                        sb.AppendLine();
                    }
                    if (order_by != null)
                    {
                        sb.Append(" ORDER BY ");
                        sb.Append(order_by);
                        sb.AppendLine();
                    }
                    sb.Append(" OFFSET ");
                    sb.Append(queryParameters.Offset);
                    sb.Append(" FETCH ");
                    sb.Append(queryParameters.Limit);
                    sb.Append(" ROWS ONLY");
                    site.Logger.LogDebug("Invoke Cargo query. Pseudo-query: {0}", sb.ToString());
                }
                var resp = await site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new
                {
                    action = "cargoquery",
                    limit = queryParameters.Limit,
                    tables,
                    fields,
                    where = queryParameters.Where,
                    group_by = queryParameters.GroupBy,
                    having = queryParameters.Having,
                    join_on,
                    order_by,
                }), cancellationToken);
                var jroot = resp["cargoquery"];
                if (jroot == null || !jroot.HasValues)
                {
                    if (jroot == null)
                    {
                        site.Logger.LogWarning("cargoquery node is missing in the response.");
                    }
#if BCL_FEATURE_ARRAY_EMPTY
                    return Array.Empty<JObject>();
#else
                    return new JObject[] { };
#endif
                }
                return ((JArray)jroot).Select(row => (JObject)row["title"]).ToList();
            }
        }

    }
}
