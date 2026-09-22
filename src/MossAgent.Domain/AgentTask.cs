namespace MossAgent.Domain;

public sealed record AgentTask(
    Guid Id,
    Guid ProjectId,
    string Title,
    ApprovalPolicy ApprovalPolicy,
    AgentTaskStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? WorktreePath);
