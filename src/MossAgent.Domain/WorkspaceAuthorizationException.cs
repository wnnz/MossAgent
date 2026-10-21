namespace MossAgent.Domain;

public sealed class WorkspaceAuthorizationException() : UnauthorizedAccessException("Workspace authorization changed.");
