using System.Collections.Immutable;
using MossAgent.Domain;

namespace MossAgent.Application.Models;

public static class ModelReasoningPolicy
{
    private static readonly IReadOnlyList<string> General = ImmutableArray.Create("none", "minimal", "low", "medium", "high", "xhigh", "max", "ultra");
    private static readonly IReadOnlyList<string> ThreeLevels = ImmutableArray.Create("low", "medium", "high");
    private static readonly IReadOnlyList<string> FourLevels = ImmutableArray.Create("minimal", "low", "medium", "high");
    private static readonly IReadOnlyList<string> ProLevels = ImmutableArray.Create("low", "high");
    private static readonly IReadOnlyList<string> ImageLevels = ImmutableArray.Create("minimal", "high");
    private static readonly IReadOnlyList<string> ProBudgets = ImmutableArray.Create("minimal", "low", "medium", "high", "xhigh", "max");
    private static readonly IReadOnlyList<string> FlashBudgets = ImmutableArray.Create("none").AddRange(ProBudgets);
    private static readonly IReadOnlyList<string> NoThinking = ImmutableArray.Create("none");
    private static readonly IReadOnlyList<string> AnthropicBudgets = ImmutableArray.Create("none", "minimal", "low", "medium", "high", "xhigh", "max");

    public static IReadOnlyList<string> SupportedEfforts(ProviderProtocol? protocol, string? modelId)
    {
        if (protocol != ProviderProtocol.GeminiGenerateContent)
        {
            var capabilities = ModelCapabilityCatalog.Find(protocol, modelId);
            if (protocol == ProviderProtocol.AnthropicMessages && capabilities is { SupportsReasoning: true, ReasoningEfforts.IsEmpty: true })
                return AnthropicBudgets;
            return capabilities is null ? General
                : capabilities.ReasoningEfforts.IsEmpty ? NoThinking : capabilities.ReasoningEfforts;
        }
        var model = modelId?.Trim() ?? string.Empty;
        if (IsFamily(model, "gemini-3.8-flash") || IsFamily(model, "gemini-3.7-flash")
            || IsFamily(model, "gemini-3.1-pro")) return ThreeLevels;
        if (IsFamily(model, "gemini-3-pro")) return ProLevels;
        if (IsFamily(model, "gemini-3.1-flash-lite-image")) return ImageLevels;
        if (model.StartsWith("gemini-3", StringComparison.OrdinalIgnoreCase)) return FourLevels;
        if (IsFamily(model, "gemini-2.5-pro")) return ProBudgets;
        if (IsFamily(model, "gemini-2.5-flash")) return FlashBudgets;
        var known = ModelCapabilityCatalog.Find(protocol, model);
        return known is not null && !known.ReasoningEfforts.IsEmpty ? known.ReasoningEfforts : NoThinking;
    }

    public static bool Supports(ProviderProtocol? protocol, string? modelId, string? effort)
    {
        var normalized = Normalize(effort);
        if (protocol is ProviderProtocol.OpenAiCompatible or ProviderProtocol.OpenAiResponses)
            return normalized.Length is > 0 and <= 64 && char.IsAsciiLetterLower(normalized[0])
                && normalized.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character is '_' or '-');
        if (protocol == ProviderProtocol.AnthropicMessages && normalized == "none") return true;
        return SupportedEfforts(protocol, modelId).Contains(normalized);
    }

    public static string DefaultEffort(ProviderProtocol? protocol, string modelId)
    {
        var supported = SupportedEfforts(protocol, modelId);
        return supported.Contains("medium") ? "medium" : supported[^1];
    }

    public static string Normalize(string? effort)
    {
        var normalized = effort?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized == "off" ? "none" : normalized;
    }

    private static bool IsFamily(string model, string family) =>
        model.StartsWith(family, StringComparison.OrdinalIgnoreCase)
        && (model.Length == family.Length || model[family.Length] == '-');
}
