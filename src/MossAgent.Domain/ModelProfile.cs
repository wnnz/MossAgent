namespace MossAgent.Domain;

public sealed record ModelProfile(
    Guid Id,
    Guid ProviderId,
    string ModelId,
    string DisplayName,
    int ContextLength,
    int MaximumOutputTokens,
    string ReasoningEffort,
    double? Temperature,
    double? TopP,
    bool SupportsImages,
    bool SupportsFiles,
    bool IsDefault,
    bool IsEnabled,
    string AdvancedParametersJson);

