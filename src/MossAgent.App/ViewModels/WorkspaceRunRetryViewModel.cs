using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Agent;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

/// <summary>
/// 失败任务的显式重试：按 <see cref="RunFailureProgress"/> 决定是否要求内联确认，
/// 随后通过正常发送路径重发持久化历史中该轮的用户原文。不提供任何自动重发。
/// </summary>
public sealed class WorkspaceRunRetryViewModel : ObservableObject, IDisposable
{
    private readonly WorkspaceTaskFactory _tasks;
    private readonly Func<AgentTask?> _selectedTask;
    private readonly Func<bool> _canSend;
    private readonly Func<AgentTask, string, Task> _send;
    private Guid? _armedTaskId;
    private string? _armedPrompt;
    private string _confirmationText = string.Empty;
    private bool _disposed;

    public WorkspaceRunRetryViewModel(WorkspaceTaskFactory tasks, Func<AgentTask?> selectedTask,
        Func<bool> canSend, Func<AgentTask, string, Task> send)
    {
        _tasks = tasks;
        _selectedTask = selectedTask;
        _canSend = canSend;
        _send = send;
        RequestRetryCommand = new GuardedAsyncRelayCommand(_ => RequestAsync(), () => IsAvailable);
        ConfirmRetryCommand = new GuardedAsyncRelayCommand(_ => ConfirmAsync(), () => IsConfirmationVisible);
        CancelRetryCommand = new RelayCommand(HideConfirmation, () => IsConfirmationVisible);
    }

    public GuardedAsyncRelayCommand RequestRetryCommand { get; }
    public GuardedAsyncRelayCommand ConfirmRetryCommand { get; }
    public IRelayCommand CancelRetryCommand { get; }
    public string RetryLabel => WorkspaceText.Get("RunRetryLabel");
    public string ConfirmLabel => WorkspaceText.Get("RunRetryConfirmLabel");
    public string CancelLabel => WorkspaceText.Get("RunRetryCancelLabel");
    public bool IsAvailable => _selectedTask()?.Status == AgentTaskStatus.Failed && _canSend();
    public bool IsConfirmationVisible => _armedTaskId is not null;
    public bool ShowsRequest => IsAvailable && !IsConfirmationVisible;
    public string ConfirmationText => _confirmationText;

    public void NotifyStateChanged()
    {
        if (_armedTaskId is not null && (_selectedTask()?.Id != _armedTaskId || !IsAvailable)) HideConfirmation();
        else NotifyState();
    }

    private async Task RequestAsync()
    {
        var task = _selectedTask();
        if (task?.Status != AgentTaskStatus.Failed || !IsAvailable) return;
        var history = await _tasks.GetMessagesAsync(task.Id, CancellationToken.None);
        if (_disposed || _selectedTask()?.Id != task.Id || _selectedTask()?.Status != AgentTaskStatus.Failed) return;
        var prompt = history.LastOrDefault(message => message.Role == MessageRole.User)?.Content;
        if (string.IsNullOrWhiteSpace(prompt)) return;
        var progress = RunFailureProgressClassifier.Classify(task.FailureReason, history);
        if (progress == RunFailureProgress.NotSubmitted)
        {
            await _send(task, prompt);
            return;
        }
        _armedTaskId = task.Id;
        _armedPrompt = prompt;
        _confirmationText = WorkspaceRunRetryText.Confirmation(progress);
        NotifyState();
    }

    private async Task ConfirmAsync()
    {
        if (_armedTaskId is not { } taskId || _armedPrompt is not { } prompt) return;
        var task = _selectedTask();
        if (task?.Id != taskId || task.Status != AgentTaskStatus.Failed || !IsAvailable)
        {
            HideConfirmation();
            return;
        }
        HideConfirmation();
        await _send(task, prompt);
    }

    private void HideConfirmation()
    {
        _armedTaskId = null;
        _armedPrompt = null;
        _confirmationText = string.Empty;
        NotifyState();
    }

    private void NotifyState()
    {
        foreach (var name in new[] { nameof(IsAvailable), nameof(ShowsRequest), nameof(IsConfirmationVisible), nameof(ConfirmationText) })
            OnPropertyChanged(name);
        RequestRetryCommand.NotifyCanExecuteChanged();
        ConfirmRetryCommand.NotifyCanExecuteChanged();
        CancelRetryCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _armedTaskId = null;
        _armedPrompt = null;
        RequestRetryCommand.Dispose();
        ConfirmRetryCommand.Dispose();
        NotifyState();
    }
}
