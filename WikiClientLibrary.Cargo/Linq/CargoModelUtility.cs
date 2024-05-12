using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace WikiClientLibrary.Cargo.Linq;

internal static class CargoModelUtility
{

    public static string ColumnNameFromProperty(MemberInfo member)
    {
        var columnAttr = member.GetCustomAttribute<ColumnAttribute>();
        return columnAttr?.Name ?? member.Name;
    }

    public static Type? GetCollectionElementType(Type collectionType)
    {
        if (!collectionType.IsConstructedGenericType)
            return null;
        var genDef = collectionType.GetGenericTypeDefinition();
        if (genDef == typeof(ICollection<>) || genDef == typeof(IList<>))
        {
            return collectionType.GenericTypeArguments[0];
        }
        return null;
    }

}