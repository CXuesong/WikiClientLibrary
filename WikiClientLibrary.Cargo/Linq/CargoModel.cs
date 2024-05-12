using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using WikiClientLibrary.Cargo.Schema;

namespace WikiClientLibrary.Cargo.Linq;

/// <summary>
/// Represents a mapping between Cargo table and CLR model (DTO) type.
/// </summary>
public class CargoModel
{

    public static CargoModel FromClrType(Type clrType, string? nameOverride = null)
    {
        var tableAttr = clrType.GetCustomAttribute<TableAttribute>();
        var fields = clrType.GetProperties()
            .Where(p => p.CanRead && p.CanWrite && p.GetMethod!.IsPublic && p.SetMethod!.IsPublic)
            .Select(p => new CargoModelProperty(p))
            .ToImmutableList();
        var tableDefinition = new CargoTableDefinition(nameOverride ?? tableAttr?.Name ?? clrType.Name, fields.Select(f => f.FieldDefinition));
        return new CargoModel(clrType, tableDefinition, fields);
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