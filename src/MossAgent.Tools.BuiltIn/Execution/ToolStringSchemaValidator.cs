using System.Text.Json;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Execution;

internal static class ToolStringSchemaValidator
{
    public static void Validate(JsonElement schema)
    {
        var minimum = ReadLength(schema, "minLength");
        var maximum = ReadLength(schema, "maxLength");
        if (minimum is not null && maximum is not null && minimum > maximum)
            throw new InvalidDataException(ToolValidationText.Format("InvertedLengthRange", "$"));
    }

    private static long? ReadLength(JsonElement schema, string keyword)
    {
        if (!schema.TryGetProperty(keyword, out var value)) return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var length) || length < 0)
            throw new InvalidDataException(ToolValidationText.Format("InvalidLengthConstraint", "$", keyword));
        return length;
    }
}
