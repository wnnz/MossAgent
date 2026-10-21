namespace MossAgent.Domain;

public sealed record ProjectRootAuthorization(Guid GrantId, DirectoryObjectIdentity Identity);
