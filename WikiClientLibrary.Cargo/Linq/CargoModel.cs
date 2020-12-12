using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using WikiClientLibrary.Cargo.Schema;

namespace WikiClientLibrary.Cargo.Linq
{

    /// <summary>
    /// Represents a mapping between Cargo table and CLR model (DTO) type.
    /// </summary>
    public class CargoModel
    {

        public static CargoModel FromClrType(Type clrType, string name = null)
        {
            var fields = clrType.GetRuntimeProperties()
                .Where(p => p.CanRead && p.CanWrite && p.GetMethod.IsPublic && p.SetMethod.IsPublic)
                .Select(p => new CargoModelProperty(p))
                .ToList();
            var tableDefinition = new CargoTableDefinition(name ?? clrType.Name, fields.Select(f => f.FieldDefinition));
            return new CargoModel(clrType, tableDefinition, new ReadOnlyCollection<CargoModelProperty>(fields));
        }

        private CargoModel(Type clrType, CargoTableDefinition tableDefinition, IReadOnlyList<CargoModelProperty> properties)
        {
            ClrType = clrType ?? throw new ArgumentNullException(nameof(clrType));
            TableDefinition = tableDefinition ?? throw new ArgumentNullException(nameof(tableDefinition));
            Properties = properties;
        }

        public string Name => TableDefinition.Name;

        public Type ClrType { get; }

        public CargoTableDefinition TableDefinition { get; }

        public IReadOnlyList<CargoModelProperty> Properties { get; }

    }

}
