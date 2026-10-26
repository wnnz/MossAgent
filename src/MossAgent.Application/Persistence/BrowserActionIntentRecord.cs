namespace MossAgent.Application.Persistence;

/// <summary>
/// One journaled mutating browser action. <see cref="TargetHash"/> is a truncated SHA-256
/// of the selector or normalized origin — never the raw selector text, typed input or URL.
/// </summary>
public sealed record BrowserActionIntentRecord(
    Guid IntentId,
    Guid SessionId,
    BrowserActionKind Kind,
    string? TargetHash,
    BrowserActionIntentState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
