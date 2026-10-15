using System.Text.Json.Nodes;

namespace MossAgent.Providers.Anthropic;

internal static class AnthropicThinkingOptions
{
    private const int MinimumThinkingBudget = 1_024;

    public static void Apply(
        JsonObject request,
        string modelId,
        string reasoningEffort,
        int maximumOutputTokens)
    {
        if (!modelId.StartsWith("claude", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!TryReadModelVersion(modelId, out var version))
        {
            if (!modelId.Contains("mythos-preview", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            version = (5, 1);
        }

        if (NormalizeEffort(reasoningEffort) is not { } effort) return;

        if (SupportsAdaptiveThinking(modelId, version))
        {
            request["thinking"] = new JsonObject { ["type"] = "adaptive" };
            request["output_config"] = new JsonObject
            {
                ["effort"] = MapSupportedEffort(modelId, effort)
            };
            return;
        }

        if (!SupportsManualThinking(version)
            || GetManualThinkingBudget(effort, maximumOutputTokens) is not { } budget)
        {
            return;
        }

        request["thinking"] = new JsonObject
        {
            ["type"] = "enabled",
            ["budget_tokens"] = budget
        };
        if (IsOpus45(modelId))
        {
            request["output_config"] = new JsonObject
            {
                ["effort"] = MapSupportedEffort(modelId, effort)
            };
        }
    }

    private static string? NormalizeEffort(string effort) => effort.Trim().ToLowerInvariant() switch
    {
        "off" or "none" or "default" => null,
        "minimal" or "lowest" or "low" => "low",
        "medium" => "medium",
        "high" => "high",
        "xhigh" => "xhigh",
        "max" => "max",
        _ => null
    };

    private static bool TryReadModelVersion(
        string modelId,
        out (int Major, int Minor) version)
    {
        var parts = modelId.ToLowerInvariant().Split(['-', '.', '_'], StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < parts.Length; index++)
        {
            if (!int.TryParse(parts[index], out var major) || major is < 3 or > 9)
            {
                continue;
            }

            var minor = index + 1 < parts.Length
                && int.TryParse(parts[index + 1], out var candidate)
                && candidate is >= 0 and < 10
                    ? candidate
                    : 0;
            version = (major, minor);
            return true;
        }

        version = default;
        return false;
    }

    private static bool SupportsAdaptiveThinking(
        string modelId,
        (int Major, int Minor) version) =>
        modelId.Contains("mythos-preview", StringComparison.OrdinalIgnoreCase)
        || version.Major > 4
        || version.Major == 4 && version.Minor >= 6;

    private static bool SupportsManualThinking((int Major, int Minor) version) =>
        version.Major == 3 && version.Minor >= 7
        || version.Major == 4 && version.Minor <= 5;

    private static int? GetManualThinkingBudget(string effort, int maximumOutputTokens)
    {
        if (maximumOutputTokens <= MinimumThinkingBudget)
        {
            return null;
        }

        var requestedBudget = effort switch
        {
            "low" => MinimumThinkingBudget,
            "medium" => 4_096,
            "high" => 8_192,
            "xhigh" or "max" => 16_000,
            _ => 0
        };
        var responseReserve = Math.Min(MinimumThinkingBudget, maximumOutputTokens / 2);
        var availableBudget = maximumOutputTokens - responseReserve;
        return requestedBudget < MinimumThinkingBudget || availableBudget < MinimumThinkingBudget
            ? null
            : Math.Min(requestedBudget, availableBudget);
    }

    private static string MapSupportedEffort(string modelId, string effort)
    {
        if (effort == "xhigh" && !SupportsXHigh(modelId)
            || effort == "max" && !SupportsMax(modelId))
        {
            return "high";
        }

        return effort;
    }

    private static bool SupportsXHigh(string modelId)
    {
        var normalized = NormalizeModelId(modelId);
        return normalized.Contains("fable-5", StringComparison.Ordinal)
            || normalized.Contains("mythos-5", StringComparison.Ordinal)
            || normalized.Contains("opus-5", StringComparison.Ordinal)
            || normalized.Contains("opus-4-7", StringComparison.Ordinal)
            || normalized.Contains("opus-4-8", StringComparison.Ordinal)
            || normalized.Contains("sonnet-5", StringComparison.Ordinal);
    }

    private static bool SupportsMax(string modelId)
    {
        var normalized = NormalizeModelId(modelId);
        return SupportsXHigh(modelId)
            || normalized.Contains("opus-4-6", StringComparison.Ordinal)
            || normalized.Contains("sonnet-4-6", StringComparison.Ordinal)
            || normalized.Contains("mythos-preview", StringComparison.Ordinal);
    }

    private static bool IsOpus45(string modelId) =>
        NormalizeModelId(modelId).Contains("opus-4-5", StringComparison.Ordinal);

    private static string NormalizeModelId(string modelId) =>
        modelId.ToLowerInvariant().Replace('.', '-');
}
