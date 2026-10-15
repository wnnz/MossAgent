using System.Collections.Immutable;

namespace MossAgent.Application.Models;

public sealed record ModelCapabilities(
    string Provider,
    string ModelId,
    int ContextLength,
    int MaximumOutputTokens,
    bool SupportsImages,
    bool SupportsFiles,
    bool SupportsReasoning,
    ImmutableList<string> ReasoningEfforts);
