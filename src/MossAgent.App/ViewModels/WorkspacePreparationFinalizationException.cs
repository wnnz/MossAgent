namespace MossAgent.App.ViewModels;

internal sealed class WorkspacePreparationFinalizationException(Guid taskId, Exception innerException)
    : Exception("The prepared task's cancellation status could not be confirmed.", innerException)
{
    public Guid TaskId { get; } = taskId;
}
