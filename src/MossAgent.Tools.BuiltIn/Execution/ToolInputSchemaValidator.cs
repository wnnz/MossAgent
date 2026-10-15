using System.Text.Json;
using Json.Schema;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Execution;

internal static class ToolInputSchemaValidator
{
    public static bool TryValidate(string schemaJson, JsonElement value, out string error, out bool schemaInvalid,
        CancellationToken cancellationToken = default)
    {
        schemaInvalid = true;
        error = ToolValidationText.Format("InvalidSchema");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (schemaJson.Length > 262_144) return false;
            using var document = JsonDocument.Parse(schemaJson);
            new ToolSchemaPolicy(document.RootElement, cancellationToken).Validate();
            var declared = document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("$schema", out var dialect) ? dialect.GetString() : null;
            var selected = ToolSchemaDialect.Select(declared);
            var options = new EvaluationOptions { OutputFormat = OutputFormat.Flag };
            if (!selected.MetaSchema.Evaluate(document.RootElement, options).IsValid) return false;
            var schema = JsonSchema.Build(document.RootElement, ToolSchemaDialect.Create(selected.Dialect, cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
            schemaInvalid = false;
            error = ToolValidationText.Format("InvalidArguments");
            if (!ToolSchemaValuePolicy.Validate(value, cancellationToken)) return false;
            var valid = schema.Evaluate(value, options).IsValid;
            cancellationToken.ThrowIfCancellationRequested();
            if (!valid) error = RequiredFieldError(document.RootElement, value) ?? error;
            return valid;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            error = ToolValidationText.Format(schemaInvalid ? "InvalidSchema" : "InvalidArguments");
            return false;
        }
    }

    private static string? RequiredFieldError(JsonElement schema, JsonElement value)
    {
        if (schema.ValueKind != JsonValueKind.Object || value.ValueKind != JsonValueKind.Object
            || !schema.TryGetProperty("required", out var required)) return null;
        foreach (var field in required.EnumerateArray())
        {
            var name = field.GetString()!;
            if (!value.TryGetProperty(name, out _) && name.Length is > 0 and <= 48
                && name.All(character => char.IsAsciiLetterOrDigit(character) || character == '_'))
                return ToolValidationText.Format("RequiredFieldMissing", name);
        }
        return null;
    }
}
