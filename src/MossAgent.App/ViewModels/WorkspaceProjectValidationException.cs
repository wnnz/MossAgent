namespace MossAgent.App.ViewModels;

internal sealed class WorkspaceProjectValidationException(WorkspaceProjectValidationFailure failure) : Exception
{
    public WorkspaceProjectValidationFailure Failure { get; } = failure;
}
