using MossAgent.Domain;

namespace MossAgent.Tools.Abstractions;

public sealed record ToolExecutionAuditRecord(
    Guid Id,
    Guid TaskId,
    string CallId,
    string ToolName,
    ToolRiskLevel? RiskLevel,
    ApprovalPolicy ApprovalPolicy,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    bool? ApprovalGranted,
    bool? IsSuccess,
    string? ErrorCode,
    int? OutputLength,
    int ArtifactCount);
