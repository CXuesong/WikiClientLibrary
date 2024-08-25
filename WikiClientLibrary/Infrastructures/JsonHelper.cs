using System.Text.Json;
using System.Text.Json.Nodes;

namespace WikiClientLibrary.Infrastructures;

public static class JsonHelper
{

    public static void InplaceMerge(JsonNode root1, JsonNode root2)
    {
        if (root1.GetValueKind() != root2.GetValueKind())
            throw new JsonException($"Attempt to merge {root2.GetValueKind()} into {root1.GetValueKind()}");

        if (root1 is JsonObject obj1 && root2 is JsonObject obj2)
        {
            if (obj2.Count == 0) return;
            var obj2Entries = obj2.ToList();
            // Detach children from obj2
            obj2.Clear();
            foreach (var (key, value2) in obj2Entries)
            {
                if (obj1.TryGetPropertyValue(key, out var value1))
                {
                    if (value1 is JsonArray && value2 is JsonArray
                        || value1 is JsonObject && value2 is JsonObject)
                    {
                        InplaceMerge(value1, value2);
                    }
                    else
                    {
                        obj1[key] = value2;
                    }
                }
                else
                {
                    obj1[key] = value2;
                }
            }
            return;
        }
        if (root1 is JsonArray arr1 && root2 is JsonArray arr2)
        {
            if (arr2.Count == 0) return;
            var arr2Items = arr2.ToList();
            // Detach children from arr2
            arr2.Clear();
            foreach (var item in arr2Items) arr1.Add(item);
            return;
        }
        throw new JsonException($"Merging {root1.GetValueKind()} is not supported.");
    }

}
