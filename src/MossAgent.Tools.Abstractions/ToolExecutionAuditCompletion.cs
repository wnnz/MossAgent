namespace MossAgent.Tools.Abstractions;

public sealed record ToolExecutionAuditCompletion(
    DateTimeOffset CompletedAt,
    bool? ApprovalGranted,
    bool IsSuccess,
    string? ErrorCode,
    int OutputLength);
