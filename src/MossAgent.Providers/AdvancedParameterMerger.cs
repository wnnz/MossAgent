using System.Text.Json;
using System.Text.Json.Nodes;
using MossAgent.Application.Models;
using MossAgent.Domain;

namespace MossAgent.Providers;

internal static class AdvancedParameterMerger
{
    public static void Merge(JsonObject root, string json, ProviderProtocol protocol)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        JsonObject? advanced;
        try
        {
            advanced = JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return;
        }

        if (advanced is null) return;
        foreach (var pair in advanced)
        {
            if (protocol == ProviderProtocol.OpenAiCompatible
                && pair.Key.Equals("max_completion_tokens", StringComparison.OrdinalIgnoreCase))
            {
                ApplyAlternateCompletionLimit(root, pair.Value);
                continue;
            }

            if (ModelParameterPolicy.IsManagedRoot(protocol, pair.Key)) continue;

            var objectName = ModelParameterPolicy.MergeableObjectName(protocol, pair.Key);
            if (objectName is null)
            {
                root[pair.Key] = pair.Value?.DeepClone();
                continue;
            }

            if (pair.Value is not JsonObject additions) continue;
            if (root[objectName] is not JsonObject target)
            {
                target = new JsonObject();
                root[objectName] = target;
            }

            foreach (var addition in additions)
            {
                if (!ModelParameterPolicy.IsManagedNested(protocol, objectName, addition.Key))
                    target[addition.Key] = addition.Value?.DeepClone();
            }
        }
    }

    private static void ApplyAlternateCompletionLimit(JsonObject root, JsonNode? value)
    {
        if (value is not JsonValue requestedValue
            || !requestedValue.TryGetValue<int>(out var requested)
            || requested < 1
            || root["max_tokens"] is not JsonValue configuredValue
            || !configuredValue.TryGetValue<int>(out var configured))
            return;

        root.Remove("max_tokens");
        root["max_completion_tokens"] = Math.Min(requested, configured);
    }
}
