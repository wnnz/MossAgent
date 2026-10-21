namespace MossAgent.Domain;

public sealed class ProjectRootAuthorizationException() : UnauthorizedAccessException("Project roots require explicit confirmation.");
