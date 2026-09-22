using MossAgent.Domain;

namespace MossAgent.Tools.Abstractions;

public sealed record ToolExecutionAuditStart(
    Guid TaskId,
    string CallId,
    string ToolName,
    ToolRiskLevel? RiskLevel,
    ApprovalPolicy ApprovalPolicy,
    DateTimeOffset StartedAt);
