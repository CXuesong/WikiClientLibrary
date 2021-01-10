using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text;

namespace WikiClientLibrary.Cargo.Linq
{

    internal static class CargoModelUtility
    {

        public static string ColumnNameFromProperty(MemberInfo member)
        {
            var columnAttr = member.GetCustomAttribute<ColumnAttribute>();
            if (columnAttr != null)
                return columnAttr.Name;
            return member.Name;
        }

    }

}
