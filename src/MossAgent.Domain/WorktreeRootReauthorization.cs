namespace MossAgent.Domain;

public sealed record WorktreeRootReauthorization(
    Guid TaskId,
    Guid ProjectId,
    Guid GrantId,
    long AuthorizationRevision,
    DirectoryObjectIdentity PreviousIdentity,
    DirectoryObjectIdentity Identity);
