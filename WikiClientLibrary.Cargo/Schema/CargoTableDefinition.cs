using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;

namespace WikiClientLibrary.Cargo.Schema
{

    /// <summary>
    /// Represents the schema of a Cargo table.
    /// </summary>
    public class CargoTableDefinition
    {

        public CargoTableDefinition(string name, IEnumerable<CargoTableFieldDefinition> fields)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (name == "")
                throw new ArgumentException("Value cannot be null or empty.", nameof(name));
            Name = name;
            Fields = new ReadOnlyCollection<CargoTableFieldDefinition>(fields.ToList());
        }

        public string Name { get; }

        public IReadOnlyList<CargoTableFieldDefinition> Fields { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            var sb = new StringBuilder(Name.Length + Fields.Count * 10);
            sb.Append(Name);
            foreach (var f in Fields)
            {
                sb.Append(" |");
                sb.Append(f);
            }
            return sb.ToString();
        }

    }

}
