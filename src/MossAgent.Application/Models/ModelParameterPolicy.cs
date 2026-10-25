using System.Text.Json;
using MossAgent.Domain;

namespace MossAgent.Application.Models;

public static class ModelParameterPolicy
{
    private static readonly HashSet<string> CommonManagedKeys =
        new(["model", "stream", "tools"], StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<ProviderProtocol, HashSet<string>> ManagedKeys =
        new Dictionary<ProviderProtocol, HashSet<string>>
        {
            [ProviderProtocol.OpenAiResponses] = new(
                ["input", "instructions", "max_output_tokens", "previous_response_id",
                    "temperature", "top_p"], StringComparer.OrdinalIgnoreCase),
            [ProviderProtocol.OpenAiCompatible] = new(
                ["messages", "system", "max_tokens", "max_completion_tokens",
                    "temperature", "top_p", "reasoning_effort"], StringComparer.OrdinalIgnoreCase),
            [ProviderProtocol.AnthropicMessages] = new(
                ["messages", "system", "max_tokens", "temperature", "top_p", "thinking"],
                StringComparer.OrdinalIgnoreCase),
            [ProviderProtocol.GeminiGenerateContent] = new(
                ["contents", "systemInstruction"], StringComparer.OrdinalIgnoreCase)
        };

    public static bool IsManagedRoot(ProviderProtocol protocol, string key) =>
        CommonManagedKeys.Contains(key)
        || ManagedKeys.TryGetValue(protocol, out var keys) && keys.Contains(key);

    public static string? MergeableObjectName(ProviderProtocol protocol, string key)
    {
        if (protocol == ProviderProtocol.OpenAiResponses
            && key.Equals("reasoning", StringComparison.OrdinalIgnoreCase))
            return "reasoning";
        if (protocol == ProviderProtocol.AnthropicMessages
            && key.Equals("output_config", StringComparison.OrdinalIgnoreCase))
            return "output_config";
        if (protocol == ProviderProtocol.GeminiGenerateContent
            && key.Equals("generationConfig", StringComparison.OrdinalIgnoreCase))
            return "generationConfig";
        return null;
    }

    public static bool IsManagedNested(ProviderProtocol protocol, string objectName, string key) =>
        protocol switch
        {
            ProviderProtocol.OpenAiResponses when objectName == "reasoning" =>
                key.Equals("effort", StringComparison.OrdinalIgnoreCase),
            ProviderProtocol.AnthropicMessages when objectName == "output_config" =>
                key.Equals("effort", StringComparison.OrdinalIgnoreCase),
            ProviderProtocol.GeminiGenerateContent when objectName == "generationConfig" =>
                key.Equals("maxOutputTokens", StringComparison.OrdinalIgnoreCase)
                || key.Equals("temperature", StringComparison.OrdinalIgnoreCase)
                || key.Equals("topP", StringComparison.OrdinalIgnoreCase)
                || key.Equals("thinkingConfig", StringComparison.OrdinalIgnoreCase),
            _ => false
        };

    /// <summary>
    /// Returns the first policy violation in an advanced-parameters JSON
    /// document, or null when every entry may merge into a protocol request.
    /// Callers must surface the violation as an actionable explanation instead
    /// of silently dropping the parameter.
    /// </summary>
    public static ModelParameterViolation? Check(
        ProviderProtocol protocol,
        string? advancedJson,
        int maximumOutputTokens)
    {
        if (string.IsNullOrWhiteSpace(advancedJson)) return null;
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(advancedJson ?? string.Empty);
        }
        catch (JsonException)
        {
            return new(ModelParameterViolationKind.NotJsonObject, null);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return new(ModelParameterViolationKind.NotJsonObject, null);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (protocol == ProviderProtocol.OpenAiCompatible
                    && property.Name.Equals("max_completion_tokens", StringComparison.OrdinalIgnoreCase))
                {
                    if (property.Value.ValueKind != JsonValueKind.Number
                        || !property.Value.TryGetInt32(out var alternateLimit)
                        || alternateLimit is < 1 || alternateLimit > maximumOutputTokens)
                        return new(ModelParameterViolationKind.AlternateCompletionLimit, property.Name);
                    continue;
                }

                if (IsManagedRoot(protocol, property.Name))
                    return new(ModelParameterViolationKind.ManagedRoot, property.Name);

                var objectName = MergeableObjectName(protocol, property.Name);
                if (objectName is null) continue;
                if (property.Value.ValueKind != JsonValueKind.Object)
                    return new(ModelParameterViolationKind.MergeableNotObject, property.Name);
                var nested = property.Value.EnumerateObject()
                    .FirstOrDefault(item => IsManagedNested(protocol, objectName, item.Name));
                if (nested.Name is not null)
                    return new(ModelParameterViolationKind.ManagedNested, nested.Name);
            }
        }

        return null;
    }
}
