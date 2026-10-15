using System.Text.Json;

namespace MossAgent.Tools.BuiltIn.Execution;

internal sealed class ToolSchemaPolicy(JsonElement root, CancellationToken cancellationToken)
{
    private readonly HashSet<JsonElement> _active = [];
    private int _visited;

    public void Validate() => Visit(root, 0);

    private void Visit(JsonElement schema, int depth)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (++_visited > 1_024 || depth > 64 || !_active.Add(schema)) throw new InvalidDataException();
        try
        {
            if (schema.ValueKind is JsonValueKind.True or JsonValueKind.False) return;
            if (schema.ValueKind != JsonValueKind.Object) throw new InvalidDataException();
            if (!schema.Equals(root) && schema.TryGetProperty("$id", out _)) throw new InvalidDataException();
            ToolStringSchemaValidator.Validate(schema);
            foreach (var property in schema.EnumerateObject())
            {
                if (property.Name is "$dynamicRef" or "$recursiveRef") throw new InvalidDataException();
                if (property.Name == "$ref")
                    Visit(ResolveReference(property.Value.GetString()!), depth + 1);
                else foreach (var child in Children(property)) Visit(child, depth + 1);
            }
        }
        finally { _active.Remove(schema); }
    }

    private static IEnumerable<JsonElement> Children(JsonProperty property)
    {
        if (property.Name is "properties" or "patternProperties" or "$defs" or "definitions" or "dependentSchemas")
        {
            foreach (var child in property.Value.EnumerateObject()) yield return child.Value;
        }
        else if (property.Name == "dependencies")
        {
            foreach (var child in property.Value.EnumerateObject())
                if (child.Value.ValueKind != JsonValueKind.Array) yield return child.Value;
        }
        else if (property.Name is "allOf" or "anyOf" or "oneOf" or "prefixItems")
        {
            foreach (var child in property.Value.EnumerateArray()) yield return child;
        }
        else if (property.Name == "items" && property.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in property.Value.EnumerateArray()) yield return child;
        }
        else if (property.Name is "items" or "additionalItems" or "additionalProperties" or "contains" or "not"
            or "if" or "then" or "else" or "propertyNames" or "unevaluatedItems" or "unevaluatedProperties" or "contentSchema")
            yield return property.Value;
    }

    private JsonElement ResolveReference(string reference)
    {
        if (!reference.StartsWith('#')) throw new InvalidDataException();
        var pointer = Uri.UnescapeDataString(reference[1..]);
        if (pointer.Length == 0) return root;
        if (!pointer.StartsWith('/')) throw new InvalidDataException();
        var current = root;
        foreach (var segment in pointer[1..].Split('/'))
        {
            var name = segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(name, out var property)) current = property;
            else if (current.ValueKind == JsonValueKind.Array && int.TryParse(name, out var index)
                && index >= 0 && index < current.GetArrayLength()) current = current[index];
            else throw new InvalidDataException();
        }
        return current;
    }
}
