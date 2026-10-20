using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Threading;
using MossAgent.Application.Git;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceRepositoryStatusViewModel(
    IGitRepositoryInfoService gitRepositoryInfo, IGitWorktreeService? worktrees = null) : ObservableObject, IDisposable, IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly SettingsOperationLifetime _operations = new();
    private readonly HashSet<Task> _pending = [];
    private string _branchLabel = WorkspaceText.Get("RepositoryStatusUnknown");
    private string _directoryLabel = WorkspaceText.Get("RepositoryNoDirectory");
    private int _refreshVersion;
    private bool _stopping;
    private Task? _stopTask;

    public string DirectoryLabel
    {
        get => _directoryLabel;
        private set => SetProperty(ref _directoryLabel, value);
    }

    public string BranchLabel
    {
        get => _branchLabel;
        private set => SetProperty(ref _branchLabel, value);
    }

    public Task RefreshAsync(ProjectProfile? project, AgentTask? task)
    {
        lock (_gate)
        {
            if (_stopping) return Task.CompletedTask;
            var operation = _operations.CreateOperation(CancellationToken.None);
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending.Add(completion.Task);
            _ = RefreshOwnedAsync(project, task, ++_refreshVersion, operation, completion);
            return completion.Task;
        }
    }

    private async Task RefreshOwnedAsync(ProjectProfile? project, AgentTask? task, int version,
        CancellationTokenSource operation, TaskCompletionSource completion)
    {
        Exception? failure = null;
        try { await ReadAsync(project, task, version, operation.Token).ConfigureAwait(false); }
        catch (Exception exception) { failure = exception; }
        finally
        {
            operation.Dispose();
            lock (_gate)
            {
                if (failure is null) completion.TrySetResult();
                else { completion.TrySetException(failure); _ = completion.Task.Exception; }
                _pending.Remove(completion.Task);
            }
        }
    }

    private async Task ReadAsync(ProjectProfile? project, AgentTask? task, int version, CancellationToken token)
    {
        if (token.IsCancellationRequested) return;
        if (project is null)
        {
            Publish(version, WorkspaceText.Get("RepositoryNoDirectory"), WorkspaceText.Get("RepositoryNoSelection"));
            return;
        }
        var directory = task?.WorktreePath ?? project.PrimaryDirectory;
        var directoryName = Path.GetFileName(Path.TrimEndingDirectorySeparator(directory));
        var label = string.IsNullOrWhiteSpace(directoryName) ? directory : directoryName;
        Publish(version, label, WorkspaceText.Get("RepositoryStatusUnknown"));
        string? branch;
        try
        {
            if (task?.WorktreePath is not null)
            {
                if (worktrees is null || task.ProjectId != project.Id) return;
                await worktrees.ValidateAsync(project, task.Id, task.WorktreePath, token).ConfigureAwait(false);
            }
            Task<string?> read;
            lock (_gate)
            {
                if (_stopping || version != _refreshVersion || token.IsCancellationRequested) return;
                read = gitRepositoryInfo.GetBranchNameAsync(directory, token);
            }
            branch = await read.ConfigureAwait(false);
        }
        catch (Exception) { return; }
        if (!token.IsCancellationRequested)
            await PublishAsync(version, label, branch ?? WorkspaceText.Get("RepositoryNotGit"), token).ConfigureAwait(false);
    }

    private async Task PublishAsync(int version, string directory, string branch, CancellationToken token)
    {
        if (Avalonia.Application.Current is null || Dispatcher.UIThread.CheckAccess())
        {
            Publish(version, directory, branch);
            return;
        }
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() => Publish(version, directory, branch),
                DispatcherPriority.Normal, token).GetTask().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private void Publish(int version, string directory, string branch)
    {
        lock (_gate)
        {
            if (_stopping || version != _refreshVersion) return;
            DirectoryLabel = directory;
            if (!_stopping && version == _refreshVersion) BranchLabel = branch;
        }
    }

    public Task StopAsync()
    {
        lock (_gate)
        {
            if (_stopTask is not null) return _stopTask;
            _stopping = true;
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _stopTask = completion.Task;
            _ = StopOwnedAsync(_pending.ToArray(), completion);
            return _stopTask;
        }
    }

    private async Task StopOwnedAsync(Task[] pending, TaskCompletionSource completion)
    {
        try
        {
            try { _operations.Dispose(); }
            finally { await Task.WhenAll(pending).ConfigureAwait(false); }
            completion.TrySetResult();
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
            _ = completion.Task.Exception;
        }
    }

    public void Dispose() => _ = StopAsync();
    public ValueTask DisposeAsync() => new(StopAsync());
}
