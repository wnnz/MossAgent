using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

/// <summary>
/// Owns the composer send action for <see cref="WorkspaceViewModel"/>: send/stop
/// eligibility behind the primary action, send and failed-run retry dispatch
/// through <see cref="WorkspaceSendPreparation"/> and <see cref="WorkspaceSendCoordinator"/>,
/// and surfacing of preparation failures on the workspace shell.
/// </summary>
internal sealed class WorkspaceSendActionCoordinator(
    WorkspaceViewModel workspace,
    WorkspaceSendPreparation preparation,
    WorkspaceRunTracker runs,
    WorkspaceSendCoordinator sender,
    Func<bool> isDisposed,
    Action<bool> setPreparationError,
    Action<string> setActivity,
    Action<string> notifyPropertyChanged)
{
    public void ExecutePrimaryAction()
    {
        if (workspace.IsRunning) Stop();
        else if (CanSend()) preparation.Start(SendAsync);
    }

    public bool CanExecutePrimaryAction() => !isDisposed() && (workspace.IsRunning ? CanStop() : CanSend());

    public bool CanSend(bool allowRunning = false) =>
        !isDisposed() && workspace.Startup.IsReady && workspace.Drafts.CanSend && workspace.Attachments.IsModelCompatible
        && !preparation.IsActive && !workspace.IsShuttingDown && !workspace.FileChanges.Undo.IsBusy && workspace.AgentPicker.CanUseSelection
        && workspace.SelectedProvider is not null
        && workspace.SelectedModel is not null && workspace.ModelPicker.CanSendWithSelectedModel
        && workspace.ModelPicker.SelectedItem?.Provider.Id == workspace.SelectedProvider.Id
        && workspace.ModelPicker.SelectedItem.Model.Id == workspace.SelectedModel.Id
        && (!string.IsNullOrWhiteSpace(workspace.ComposerText) || workspace.Attachments.HasImageItems)
        && (workspace.Catalog.SelectedTask is null || (workspace.Catalog.SelectedTask.ProjectId == workspace.Catalog.SelectedProject?.Id
            && (allowRunning || !runs.IsRunning(workspace.Catalog.SelectedTask.Id))
            && workspace.Queue?.IsPreparing(workspace.Catalog.SelectedTask.Id) != true
            && !workspace.Catalog.TaskManagement.BlocksTaskStart(workspace.Catalog.SelectedTask.Id)
            && !workspace.Catalog.WorktreeAuthorization.BlocksTaskStart(workspace.Catalog.SelectedTask.Id)));

    public bool CanRetrySend() =>
        !isDisposed() && workspace.Startup.IsReady && workspace.Drafts.CanSend && workspace.Attachments.IsModelCompatible && !preparation.IsActive
        && !workspace.IsShuttingDown && !workspace.FileChanges.Undo.IsBusy && workspace.AgentPicker.CanUseSelection
        && workspace.SelectedProvider is not null && workspace.SelectedModel is not null && workspace.ModelPicker.CanSendWithSelectedModel
        && workspace.ModelPicker.SelectedItem?.Provider.Id == workspace.SelectedProvider.Id
        && workspace.ModelPicker.SelectedItem.Model.Id == workspace.SelectedModel.Id
        && workspace.Catalog.SelectedTask is { Status: AgentTaskStatus.Failed } task
        && task.ProjectId == workspace.Catalog.SelectedProject?.Id && !runs.IsRunning(task.Id)
        && workspace.Queue?.IsPreparing(task.Id) != true
        && !workspace.Catalog.TaskManagement.BlocksTaskStart(task.Id)
        && !workspace.Catalog.WorktreeAuthorization.BlocksTaskStart(task.Id);

    public Task SendRetryAsync(AgentTask task, string prompt)
    {
        var request = WorkspaceSendRequest.Capture(workspace) with
        {
            Draft = prompt,
            ExistingTask = task,
            Attachments = [],
            UseWorktree = false,
            WorktreeBaseReference = null
        };
        return preparation.Start(cancellationToken => SendRequestAsync(request, cancellationToken));
    }

    private bool CanStop() =>
        workspace.HasActiveRun || preparation.CanCancel
        || workspace.Catalog.SelectedTask is { } task && workspace.Queue.IsPreparing(task.Id);

    private void Stop()
    {
        if (workspace.HasActiveRun && workspace.Catalog.SelectedTask is { } task) runs.Cancel(task.Id);
        else preparation.Cancel();
        if (workspace.Catalog.SelectedTask is { } selected) workspace.Queue.CancelPreparation(selected.Id);
    }

    private async Task SendAsync(CancellationToken cancellationToken) =>
        await SendRequestAsync(WorkspaceSendRequest.Capture(workspace), cancellationToken);

    private async Task SendRequestAsync(WorkspaceSendRequest request, CancellationToken cancellationToken)
    {
        workspace.Catalog.Environment.ClearPreparationNotice();
        setPreparationError(false);
        try
        {
            await sender.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!isDisposed()) setActivity(WorkspaceText.Get("PreparationCancelled"));
        }
        catch (Exception exception)
        {
            if (isDisposed()) throw new InvalidOperationException("Task finalization failed.");
            workspace.Catalog.Environment.ReportPreparationFailure(exception);
            notifyPropertyChanged(nameof(WorkspaceViewModel.SendPreparationError));
            setPreparationError(true);
            setActivity(workspace.SendPreparationError);
            if (workspace.IsShuttingDown) throw new InvalidOperationException("Task finalization failed.");
        }
    }
}
