namespace MossAgent.Domain;

public sealed record WorktreeRootAuthorization(
    Guid TaskId, Guid ProjectId, Guid GrantId, DirectoryObjectIdentity Identity);
