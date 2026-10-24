using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal sealed class WorkspaceWorktreeCleanupController : ObservableObject
{
    private readonly WorkspaceWorktreeCleanupService _service;
    private readonly Func<ProjectProfile?> _project;
    private readonly Func<AgentTask?> _task;
    private readonly Func<AgentTask?, bool> _canRun;
    private readonly Action<AgentTask> _apply;
    private readonly Action<string> _status;
    private Guid? _confirmationTaskId;

    public WorkspaceWorktreeCleanupController(WorkspaceWorktreeCleanupService service,
        Func<ProjectProfile?> project, Func<AgentTask?> task, Func<AgentTask?, bool> canRun,
        Action<AgentTask> apply, Action<string> status)
    {
        _service = service;
        _project = project;
        _task = task;
        _canRun = canRun;
        _apply = apply;
        _status = status;
        Command = new AsyncRelayCommand(CleanupAsync, () => canRun(task()));
    }

    public IAsyncRelayCommand Command { get; }
    public string Text => WorkspaceText.Get(_confirmationTaskId == _task()?.Id
        ? "WorktreeCleanupConfirm" : "WorktreeCleanupAction");

    public void NotifyAvailability() => Command.NotifyCanExecuteChanged();

    public void CancelConfirmation()
    {
        if (_confirmationTaskId is null) return;
        _confirmationTaskId = null;
        OnPropertyChanged(nameof(Text));
    }

    private async Task CleanupAsync()
    {
        if (_project() is not { } project || _task() is not { WorktreePath: not null } task)
        {
            return;
        }
        if (_confirmationTaskId != task.Id)
        {
            _confirmationTaskId = task.Id;
            OnPropertyChanged(nameof(Text));
            _status(WorkspaceText.Get("WorktreeCleanupConfirmHint"));
            return;
        }
        try
        {
            _apply(await _service.CleanupAsync(project, task, CancellationToken.None));
            _status(WorkspaceText.Get("WorktreeCleanupSuccess"));
        }
        catch (Exception)
        {
            _status(WorkspaceText.Get("TaskEnvironmentCleanupFailure"));
        }
        finally
        {
            CancelConfirmation();
            NotifyAvailability();
        }
    }
}
