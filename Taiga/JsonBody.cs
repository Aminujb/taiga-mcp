using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TaigaMcp.Taiga;

/// <summary>
/// Helper for building JSON request bodies.
/// Builds an object conveniently and skips null values automatically,
/// so PATCH requests don't overwrite Taiga fields with empty values.
/// </summary>
public static class JsonBody
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Creates a JsonObject from name/value pairs, skipping nulls.</summary>
    public static JsonObject Object(params (string Name, object? Value)[] items)
    {
        var obj = new JsonObject();
        foreach (var (name, value) in items)
        {
            if (value is null)
            {
                continue;
            }

            obj[name] = ToNode(value);
        }

        return obj;
    }

    /// <summary>Converts an arbitrary value to a JsonNode.</summary>
    public static JsonNode? ToNode(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is JsonNode node)
        {
            return node.DeepClone();
        }

        return JsonSerializer.SerializeToNode(value, Options);
    }
}
