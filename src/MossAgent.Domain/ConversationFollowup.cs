namespace MossAgent.Domain;

public sealed record ConversationFollowup(
    Guid Id,
    Guid TaskId,
    Guid? ProjectId,
    string Prompt,
    ModelProfile Model,
    Guid? AgentProfileId,
    string? AgentInstructions,
    ApprovalPolicy ApprovalPolicy,
    IReadOnlyList<string> AuthorizedRoots,
    string? WorktreePath,
    IReadOnlyList<ConversationAttachment> Attachments,
    DateTimeOffset CreatedAt);
