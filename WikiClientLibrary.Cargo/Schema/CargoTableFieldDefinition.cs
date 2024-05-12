using System.Numerics;
using System.Reflection;
using WikiClientLibrary.Cargo.Linq;
using WikiClientLibrary.Cargo.Schema.DataAnnotations;

namespace WikiClientLibrary.Cargo.Schema;

/// <summary>
/// Represents a field in <see cref="CargoTableDefinition"/>.
/// </summary>
public class CargoTableFieldDefinition
{

    internal static CargoTableFieldType MatchFieldType(Type clrType, out bool isCollection, out bool isNullable)
    {
        isCollection = false;
        isNullable = !clrType.IsValueType;
        {
            var elementType = CargoModelUtility.GetCollectionElementType(clrType);
            if (elementType != null)
            {
                isCollection = true;
                clrType = elementType;
            }
        }
        if (clrType.IsConstructedGenericType)
        {
            var genDef = clrType.GetGenericTypeDefinition();
            if (genDef == typeof(Nullable<>))
            {
                // Nullable<> is value type, but it's nullable.
                isNullable = true;
                clrType = clrType.GenericTypeArguments[0];
            }
        }
        if (clrType == typeof(string)) return CargoTableFieldType.String;
        if (clrType == typeof(bool)) return CargoTableFieldType.Boolean;
        if (
            clrType == typeof(BigInteger)
            || clrType == typeof(byte) || clrType == typeof(sbyte)
            || clrType == typeof(short) || clrType == typeof(ushort)
            || clrType == typeof(int) || clrType == typeof(uint)
            || clrType == typeof(long) || clrType == typeof(ulong)
        ) return CargoTableFieldType.Integer;
        if (clrType == typeof(decimal)
            || clrType == typeof(float) || clrType == typeof(double)
           ) return CargoTableFieldType.Integer;
        if (clrType == typeof(DateTime) || clrType == typeof(DateTimeOffset)) return CargoTableFieldType.Datetime;
        throw new NotSupportedException($"Not supported type {clrType}.");
    }

    internal static CargoTableFieldDefinition FromProperty(PropertyInfo property)
    {
        var fieldType = MatchFieldType(property.PropertyType, out var isCollection, out var isNullable);
        var listAttr = property.GetCustomAttribute<CargoListAttribute>();
        if (listAttr != null && !isCollection)
            throw new InvalidOperationException("Invalid CargoListAttribute usage: Annotated member is not collection.");
        return new CargoTableFieldDefinition(CargoModelUtility.ColumnNameFromProperty(property), fieldType,
            isCollection ? listAttr?.Delimiter ?? "," : null, !isNullable);
    }

    public CargoTableFieldDefinition(string name, CargoTableFieldType fieldType, string? listDelimiter, bool isMandatory)
    {
        Name = name;
        FieldType = fieldType;
        ListDelimiter = listDelimiter;
        IsMandatory = isMandatory;
    }

    public string Name { get; }

    public CargoTableFieldType FieldType { get; }

    /// <summary>Whether this field is of List type.</summary>
    public bool IsList => ListDelimiter != null;

    /// <summary>For fields of List type, this is the delimiter of the list item.</summary>
    /// <value>delimiter of the list item, or <c>null</c> if this field is not of List type.</value>
    public string? ListDelimiter { get; }

    /// <summary>
    /// If set, the field is declared as mandatory, i.e. blank values are not allowed.
    /// </summary>
    public bool IsMandatory { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        if (IsList)
        {
            return $"{Name}=List ({ListDelimiter}) of {FieldType}";
        }
        return $"{Name}={FieldType}";
    }

}

public enum CargoTableFieldType
{

    /// <summary>Holds the name of a page in the wiki (default max size: 300 characters)</summary>
    Page,

    /// <summary>Holds standard, non-wikitext text (default max size: 300 characters)</summary>
    String,

    /// <summary>Holds standard, non-wikitext text; intended for longer values (Not indexed)</summary>
    Text,

    /// <summary>Holds an integer</summary>
    Integer,

    /// <summary>Holds a real, i.e. non-integer, number</summary>
    Float,

    /// <summary>Holds a date without time</summary>
    Date,
    StartDate,

    /// <summary>Similar to Date, but are meant to Hold the beginning and end of some duration. A table can hold either no Start date and no End date field, or exactly one of both.</summary>
    EndDate,

    /// <summary>Holds a date and time</summary>
    Datetime,
    StartDatetime,

    /// <summary>Work like Start date or End date, but include a time.</summary>
    EndDatetime,

    /// <summary>Holds a Boolean value, whose value should be 1 or 0, or 'yes' or 'no' (see this section for Cargo-specific information on querying Boolean values)</summary>
    Boolean,

    /// <summary>Holds geographical coordinates</summary>
    Coordinates,

    /// <summary>Holds a short text that is meant to be parsed by the MediaWiki parser (default max size: 300 characters)</summary>
    WikitextString,

    /// <summary>Holds longer text that is meant to be parsed by the MediaWiki parser (Not indexed)</summary>
    Wikitext,

    /// <summary>Holds text that can be searched on, using the MATCHES command (requires MySQL 5.6+ or MariaDB 5.6+)</summary>
    Searchtext,

    /// <summary>Holds the name of an uploaded file or image in the wiki (similar to Page, but does not require specifying the "File:" namespace) (default max size: 300 characters)</summary>
    File,

    /// <summary>Holds a URL (default max size: 300 characters)</summary>
    URL,

    /// <summary>Holds an email address (default max size: 300 characters)</summary>
    Email,

    /// <summary>Holds a "rating" value, i.e. usually an integer from 1 to 5</summary>
    Rating,

}
