using System.Text.Json;

namespace MossAgent.Tools.BuiltIn.Execution;

internal static class ToolSchemaValuePolicy
{
    public static bool Validate(JsonElement value, CancellationToken cancellationToken)
    {
        if (value.ValueKind == JsonValueKind.Undefined || value.GetRawText().Length > 2_097_152) return false;
        var remaining = 8_192;
        return Visit(value, ref remaining, 0, cancellationToken);
    }

    private static bool Visit(JsonElement value, ref int remaining, int depth, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (--remaining < 0 || depth > 64) return false;
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
                if (!Visit(property.Value, ref remaining, depth + 1, cancellationToken)) return false;
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
                if (!Visit(item, ref remaining, depth + 1, cancellationToken)) return false;
        }
        return true;
    }
}
