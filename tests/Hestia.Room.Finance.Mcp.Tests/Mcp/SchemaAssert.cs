using System.Text.Json.Nodes;

namespace Hestia.Room.Finance.Mcp.Tests.Mcp;

/// <summary>
/// Checks structured content against the output schema a tool advertises, recursively: the parts of JSON Schema the
/// generated schemas use (type, properties, required, items, enum, nullable types). Enough to catch a property
/// dropped or renamed at any depth, without a JSON Schema library.
/// </summary>
internal static class SchemaAssert
{
    public static void Matches(JsonNode schema, JsonNode? value, string path = "$")
    {
        var types = Types(schema);
        if (value is null)
        {
            Assert.True(types.Contains("null"), $"{path}: null, but the schema type is [{string.Join(", ", types)}].");
            return;
        }

        if (schema["enum"] is JsonArray allowed)
        {
            Assert.Contains(value.ToJsonString(), allowed.Select(a => a?.ToJsonString()));
        }

        switch (value)
        {
            case JsonObject obj:
                Assert.True(types.Contains("object"), $"{path}: an object, but the schema type is [{string.Join(", ", types)}].");
                var properties = schema["properties"]?.AsObject() ?? [];
                foreach (var required in schema["required"]?.AsArray() ?? [])
                {
                    Assert.True(obj.ContainsKey((string)required!), $"{path}: required property '{required}' is missing.");
                }

                foreach (var (name, child) in obj)
                {
                    Assert.True(properties.ContainsKey(name), $"{path}: property '{name}' is not in the schema.");
                    Matches(properties[name]!, child, $"{path}.{name}");
                }

                break;
            case JsonArray array:
                Assert.True(types.Contains("array"), $"{path}: an array, but the schema type is [{string.Join(", ", types)}].");
                for (var i = 0; i < array.Count; i++)
                {
                    Matches(schema["items"]!, array[i], $"{path}[{i}]");
                }

                break;
            default:
                var kind = value.GetValueKind() switch
                {
                    System.Text.Json.JsonValueKind.String => "string",
                    System.Text.Json.JsonValueKind.Number => "number",
                    _ => "boolean",
                };
                // An integer is also a number.
                Assert.True(types.Contains(kind) || (kind == "number" && types.Contains("integer")),
                    $"{path}: a {kind}, but the schema type is [{string.Join(", ", types)}].");
                break;
        }
    }

    private static HashSet<string> Types(JsonNode schema) => schema["type"] switch
    {
        JsonArray many => [.. many.Select(t => (string)t!)],
        JsonValue one => [(string)one!],
        // No type: anything goes (e.g. an enum only).
        _ => ["object", "array", "string", "number", "integer", "boolean", "null"],
    };
}
