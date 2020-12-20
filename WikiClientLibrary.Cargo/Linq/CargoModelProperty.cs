using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using WikiClientLibrary.Cargo.Schema;

namespace WikiClientLibrary.Cargo.Linq
{

    /// <summary>
    /// Represents a property (or field) in <see cref="CargoModel"/>.
    /// </summary>
    [DebuggerDisplay("{Name} ({ClrProperty})")]
    public class CargoModelProperty
    {

        internal CargoModelProperty(PropertyInfo clrProperty)
        {
            ClrProperty = clrProperty;
            ClrType = clrProperty.PropertyType;
            FieldDefinition = CargoTableFieldDefinition.FromProperty(clrProperty);
        }

        /// <summary>Cargo field name in the Cargo table.</summary>
        public string Name => FieldDefinition.Name;

        public PropertyInfo ClrProperty { get; }

        public Type ClrType { get; }

        public CargoTableFieldDefinition FieldDefinition { get; }

    }

}
