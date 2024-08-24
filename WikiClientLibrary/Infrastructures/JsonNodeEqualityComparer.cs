using System.Diagnostics;
using System.Text.Json.Nodes;

namespace WikiClientLibrary.Infrastructures;

// Not publicize this comparer -- we really need some official GetHashCode impl.
internal class JsonNodeEqualityComparer : EqualityComparer<JsonNode>
{

    public static new JsonNodeEqualityComparer Default { get; } = new ();

    /// <inheritdoc />
    public override bool Equals(JsonNode? x, JsonNode? y) => x == y || JsonNode.DeepEquals(x, y);

    /// <inheritdoc />
    public override int GetHashCode(JsonNode rootObj)
    {
        Debug.Assert(rootObj != null);
        var hashCode = new HashCode();
        AddHashCode(rootObj);
        return hashCode.ToHashCode();

        void AddHashCode(JsonNode node)
        {
            switch (node)
            {
                case JsonArray arr:
                    hashCode.Add(1);
                    hashCode.Add(arr.Count);
                    foreach (var n in arr)
                    {
                        if (n != null)
                            AddHashCode(n);
                        else
                            hashCode.Add(0);
                    }
                    break;
                case JsonObject obj:
                    hashCode.Add(2);
                    hashCode.Add(obj.Count);
                    // See JsonPropertyDictionary[T]..ctor
                    var comparer = obj.Options?.PropertyNameCaseInsensitive == true ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
                    foreach (var (k, v) in obj)
                    {
                        hashCode.Add(k, comparer);
                        if (v != null)
                            AddHashCode(v);
                        else
                            hashCode.Add(0);
                    }
                    break;
                case JsonValue val:
                    hashCode.Add(3);
                    if (val.TryGetValue(out string? vs)) hashCode.Add(vs, StringComparer.Ordinal);
                    else if (val.TryGetValue(out bool vb)) hashCode.Add(vb);
                    else if (val.TryGetValue(out byte vi8)) hashCode.Add(vi8);
                    else if (val.TryGetValue(out short vi16)) hashCode.Add(vi16);
                    else if (val.TryGetValue(out int vi32)) hashCode.Add(vi32);
                    else if (val.TryGetValue(out long vi64)) hashCode.Add(vi64);
                    else if (val.TryGetValue(out float vf)) hashCode.Add(vf);
                    else if (val.TryGetValue(out double vd)) hashCode.Add(vd);
                    else if (val.TryGetValue(out DateTime vdt)) hashCode.Add(vdt);
                    else if (val.TryGetValue(out DateTimeOffset vdto)) hashCode.Add(vdto);
                    // TODO potential boxing
                    else hashCode.Add(val.GetValue<object>());
                    break;
                default:
                    // While we can still try our best to handle such situation,
                    // we might want to update the library to hash the new JSON node type properly.
                    Debug.Fail($"Unexpected JsonNode type: {node.GetType()}.");
                    hashCode.Add(100);
                    break;
            }
        }
    }

}
