using System;
using System.Collections.Generic;
using System.Text;

namespace WikiClientLibrary.Cargo.Schema.DataAnnotations
{

    /// <summary>
    /// Annotates the property as a cargo list, i.e. <c>List (<see cref="Delimiter"/>) of [type]</c>.
    /// </summary>
    /// <remarks>
    /// All the collection properties without <see cref="CargoListAttribute"/>
    /// will use default settings, i.e. using comma (,) as list separator.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class CargoListAttribute : Attribute
    {

        public const string DefaultDelimiter = ",";

        /// <summary>Initialize the attribute with comma (,) as list separator.</summary>
        public CargoListAttribute()
            : this(DefaultDelimiter)
        {
        }

        public CargoListAttribute(string delimiter)
        {
            Delimiter = delimiter ?? throw new ArgumentNullException(nameof(delimiter));
        }

        public string Delimiter { get; }

    }

}
