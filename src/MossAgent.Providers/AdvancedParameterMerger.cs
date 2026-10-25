using System.Text.Json;
using System.Text.Json.Nodes;
using MossAgent.Application.Models;
using MossAgent.Domain;

namespace MossAgent.Providers;

internal static class AdvancedParameterMerger
{
    public static void Merge(JsonObject root, string json, ProviderProtocol protocol, int maximumOutputTokens)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        JsonObject? advanced;
        try
        {
            advanced = JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            throw new ModelCapabilityException(ModelCapabilityException.InvalidModelParameters,
                "The model's advanced parameters are not a valid JSON object. Fix them in model settings.");
        }

        if (advanced is null)
        {
            if (json.Trim() is "{}" or "") return;
            throw new ModelCapabilityException(ModelCapabilityException.InvalidModelParameters,
                "The model's advanced parameters are not a valid JSON object. Fix them in model settings.");
        }

        foreach (var pair in advanced)
        {
            if (protocol == ProviderProtocol.OpenAiCompatible
                && pair.Key.Equals("max_completion_tokens", StringComparison.OrdinalIgnoreCase))
            {
                ApplyAlternateCompletionLimit(root, pair.Key, pair.Value, maximumOutputTokens);
                continue;
            }

            if (ModelParameterPolicy.IsManagedRoot(protocol, pair.Key))
            {
                throw new ModelCapabilityException(ModelCapabilityException.InvalidModelParameters,
                    $"Advanced parameter '{pair.Key}' overrides a field managed by the application. "
                    + "Remove it in model settings.");
            }

            var objectName = ModelParameterPolicy.MergeableObjectName(protocol, pair.Key);
            if (objectName is null)
            {
                root[pair.Key] = pair.Value?.DeepClone();
                continue;
            }

            if (pair.Value is not JsonObject additions)
            {
                throw new ModelCapabilityException(ModelCapabilityException.InvalidModelParameters,
                    $"Advanced parameter '{pair.Key}' must be a JSON object. Fix it in model settings.");
            }

            if (root[objectName] is not JsonObject target)
            {
                target = new JsonObject();
                root[objectName] = target;
            }

            foreach (var addition in additions)
            {
                if (ModelParameterPolicy.IsManagedNested(protocol, objectName, addition.Key))
                {
                    throw new ModelCapabilityException(ModelCapabilityException.InvalidModelParameters,
                        $"Advanced parameter '{addition.Key}' overrides a field managed by the application. "
                        + "Remove it in model settings.");
                }

                target[addition.Key] = addition.Value?.DeepClone();
            }
        }
    }

    private static void ApplyAlternateCompletionLimit(
        JsonObject root, string key, JsonNode? value, int maximumOutputTokens)
    {
        if (value is not JsonValue requestedValue
            || !requestedValue.TryGetValue<int>(out var requested)
            || requested < 1 || requested > maximumOutputTokens
            || root["max_tokens"] is not JsonValue configuredValue
            || !configuredValue.TryGetValue<int>(out var configured))
        {
            throw new ModelCapabilityException(ModelCapabilityException.InvalidModelParameters,
                $"Advanced parameter '{key}' must be a positive integer within the configured "
                + "output limit. Fix it in model settings.");
        }

        root.Remove("max_tokens");
        root["max_completion_tokens"] = requested;
    }
}
