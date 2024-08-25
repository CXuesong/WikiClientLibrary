using System.Globalization;
using System.Text.Json;

namespace WikiClientLibrary.Infrastructures;

// Also leveraged by Cargo
/// <summary>
/// Infrastructure. The implementation is subject to changes so it's not recommended to directly reference them in your code.
/// </summary>
public static class WikiJsonElementHelper
{
    public static T? ConvertTo<T>(JsonElement e)
    {
        // TODO Eliminate boxing
        if (typeof(T) == typeof(string)) return (T?)(object?)ConvertToString(e);
        if (typeof(T) == typeof(int)) return (T)(object)ConvertToInt32(e);
        if (typeof(T) == typeof(long)) return (T)(object)ConvertToInt64(e);
        if (typeof(T) == typeof(ulong)) return (T)(object)ConvertToUInt64(e);
        if (typeof(T) == typeof(float)) return (T)(object)ConvertToSingle(e);
        if (typeof(T) == typeof(double)) return (T)(object)ConvertToDouble(e);
        if (typeof(T) == typeof(DateTime)) return (T)(object)ConvertToDateTime(e);
        throw new InvalidOperationException($"Converting JsonElement into {typeof(T)} is not supported.");
    }

    public static string? ConvertToString(JsonElement e)
    {
        return e.ValueKind switch
        {
            JsonValueKind.String or JsonValueKind.Null => e.GetString(),
            JsonValueKind.Number => e.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            var k => throw new InvalidOperationException($"Invalid conversion from JSON {k} to String."),
        };
    }

    public static int ConvertToInt32(JsonElement e)
    {
        return e.ValueKind switch
        {
            JsonValueKind.String => Convert.ToInt32(e.GetString(), CultureInfo.InvariantCulture),
            JsonValueKind.Number => e.GetInt32(),
            var k => throw new InvalidOperationException($"Invalid conversion from JSON {k} to Int32."),
        };
    }

    public static long ConvertToInt64(JsonElement e)
    {
        return e.ValueKind switch
        {
            JsonValueKind.String => Convert.ToInt64(e.GetString(), CultureInfo.InvariantCulture),
            JsonValueKind.Number => e.GetInt64(),
            var k => throw new InvalidOperationException($"Invalid conversion from JSON {k} to Int64."),
        };
    }

    public static ulong ConvertToUInt64(JsonElement e)
    {
        return e.ValueKind switch
        {
            JsonValueKind.String => Convert.ToUInt64(e.GetString(), CultureInfo.InvariantCulture),
            JsonValueKind.Number => e.GetUInt64(),
            var k => throw new InvalidOperationException($"Invalid conversion from JSON {k} to UInt64."),
        };
    }

    public static float ConvertToSingle(JsonElement e)
    {
        return e.ValueKind switch
        {
            JsonValueKind.String => Convert.ToSingle(e.GetString(), CultureInfo.InvariantCulture),
            JsonValueKind.Number => e.GetSingle(),
            var k => throw new InvalidOperationException($"Invalid conversion from JSON {k} to Single."),
        };
    }

    public static double ConvertToDouble(JsonElement e)
    {
        return e.ValueKind switch
        {
            JsonValueKind.String => Convert.ToDouble(e.GetString(), CultureInfo.InvariantCulture),
            JsonValueKind.Number => e.GetDouble(),
            var k => throw new InvalidOperationException($"Invalid conversion from JSON {k} to Double."),
        };
    }

    public static bool TryConvertToDouble(JsonElement e, out double value)
    {
        value = default;
        return e.ValueKind switch
        {
            JsonValueKind.String => double.TryParse(e.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture, out value),
            JsonValueKind.Number => e.TryGetDouble(out value),
            _ => false,
        };
    }

    public static DateTime ConvertToDateTime(JsonElement e)
    {
        return e.ValueKind switch
        {
            JsonValueKind.String => Convert.ToDateTime(e.GetString(), CultureInfo.InvariantCulture),
            var k => throw new InvalidOperationException($"Invalid conversion from JSON {k} to DateTime."),
        };
    }
}
