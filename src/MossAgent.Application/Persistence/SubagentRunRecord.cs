namespace MossAgent.Application.Persistence;

/// <summary>
/// One journaled parent→child subagent delegation. <see cref="SnapshotJson"/> stores the
/// bounded resolved-configuration snapshot (profile, provider, model, effort, sharing and
/// task preview) captured before the child model call.
/// </summary>
public sealed record SubagentRunRecord(
    Guid RunId,
    Guid TaskId,
    string ParentCallId,
    string ProfileName,
    string SnapshotJson,
    SubagentRunStatus Status,
    string? ErrorCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
