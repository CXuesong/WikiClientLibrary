using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WikiClientLibrary.Cargo.Linq
{
    /// <summary>
    /// Contains stub methods used inside expressions in <see cref="IQueryable{T}"/> extension methods.
    /// These methods are used for building LINQ query expressions and are not intended to be invoked directly.
    /// </summary>
    public static class CargoFunctions
    {

        private static Exception GetClientInvocationException(string name)
        {
            return new InvalidOperationException($"Cargo LINQ function {name} should be used inside Cargo LINQ query expression.");
        }

        public static bool Like(string value, string pattern)
            => throw GetClientInvocationException(nameof(Like));

        /// <summary>
        /// Determines whether the list-typed field contains a certain value.
        /// (See <a href="https://www.mediawiki.org/wiki/Extension:Cargo/Querying_data#The_%22HOLDS%22_command">mw:Extension:Cargo/Querying data#The "HOLDS" command</a>.)
        /// </summary>
        /// <typeparam name="T">element name.</typeparam>
        /// <param name="cargoList">Cargo table field expression of type <c>list</c>.</param>
        /// <param name="element">the matching element.</param>
        /// <returns>whether the list contains the matching element.</returns>
        public static bool Holds<T>(IEnumerable<T> cargoList, T element)
            => throw GetClientInvocationException(nameof(Holds));

        /// <summary>
        /// Determines whether the list-typed field contains any value matching the specific <c>LIKE</c> wildcard expression.
        /// (See <a href="https://www.mediawiki.org/wiki/Extension:Cargo/Querying_data#HOLDS_LIKE">mw:Extension:Cargo/Querying data#HOLDS LIKE</a>.)
        /// </summary>
        /// <typeparam name="T">element name.</typeparam>
        /// <param name="cargoList">Cargo table field expression of type <c>list</c>.</param>
        /// <param name="pattern">the matching pattern with <c>%</c> or <c>_</c> wildcard.</param>
        /// <returns>whether the list contains the matching element.</returns>
        public static bool HoldsLike<T>(IEnumerable<T> cargoList, string pattern)
            => throw GetClientInvocationException(nameof(HoldsLike));

        /// <summary>
        /// Returns the difference (<paramref name="dt1"/> - <paramref name="dt2"/>) between 2 dates expressed as a value in days from one date to the other.
        /// </summary>
        /// <param name="dt1">date 1.</param>
        /// <param name="dt2">date 2.</param>
        public static int DateDiff(DateTime? dt1, DateTime? dt2)
            => throw GetClientInvocationException(nameof(DateDiff));

        /// <summary>
        /// Returns the difference (<paramref name="dt1"/> - <paramref name="dt2"/>) between 2 dates expressed as a value in days from one date to the other.
        /// </summary>
        /// <param name="dt1">date 1.</param>
        /// <param name="dt2">date 2.</param>
        public static int DateDiff(DateTimeOffset? dt1, DateTimeOffset? dt2)
            => throw GetClientInvocationException(nameof(DateDiff));

    }
}
