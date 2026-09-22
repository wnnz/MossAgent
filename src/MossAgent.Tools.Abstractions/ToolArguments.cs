using System.Text.Json;

namespace MossAgent.Tools.Abstractions;

public static class ToolArguments
{
    public static string GetRequiredString(this JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new InvalidOperationException($"参数 {name} 必须是非空字符串。");
        }

        return property.GetString()!;
    }

    public static string? GetOptionalString(this JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
    }

    public static int? GetOptionalInt32(this JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var property) && property.TryGetInt32(out var value)
            ? value
            : null;
    }

    public static long? GetOptionalInt64(this JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var property) && property.TryGetInt64(out var value)
            ? value
            : null;
    }

    public static bool? GetOptionalBoolean(this JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var property)
            && property.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? property.GetBoolean()
                : null;
    }
}

