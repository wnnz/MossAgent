namespace MossAgent.Domain;

public sealed class ProjectAuthorizationConflictException() : InvalidOperationException("Project authorization has changed; refresh before saving.");
