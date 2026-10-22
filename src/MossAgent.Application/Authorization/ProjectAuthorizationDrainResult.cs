namespace MossAgent.Application.Authorization;

public readonly record struct ProjectAuthorizationDrainResult(bool IsDrained, int OutstandingOperations);
