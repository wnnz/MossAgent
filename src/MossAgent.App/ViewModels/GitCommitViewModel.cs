using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.App.Resources;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;

namespace MossAgent.App.ViewModels;

public sealed class GitCommitViewModel : ObservableObject, IDisposable, IAsyncDisposable
{
    private readonly IToolExecutor _tools;
    private readonly Func<CancellationToken, Task> _refresh;
    private readonly Dictionary<Guid, string> _drafts = [];
    private readonly CancellationTokenSource _shutdown = new();
    private CancellationTokenSource _selection = new();
    private CancellationTokenSource? _operation;
    private ToolExecutionContext? _context;
    private GitCommitPreview? _preview;
    private string _message = string.Empty;
    private string _status = string.Empty;
    private bool _open;
    private bool _busy;
    private bool _disposed;
    private int _generation;

    public GitCommitViewModel(IToolExecutor tools, Func<CancellationToken, Task> refresh)
    {
        _tools = tools;
        _refresh = refresh;
        OpenCommand = new(OpenAsync, () => CanStart && !IsOpen);
        RefreshPreviewCommand = new(token => RunAsync(false, token), () => CanStart && IsOpen);
        CommitCommand = new(token => RunAsync(true, token), () => CanCommit);
        CloseCommand = new RelayCommand(() => { IsOpen = false; NotifyState(); }, () => !_disposed && IsOpen && !IsBusy);
        CancelCommand = new RelayCommand(Cancel, () => CanCancel);
    }

    public GuardedAsyncRelayCommand OpenCommand { get; }
    public GuardedAsyncRelayCommand RefreshPreviewCommand { get; }
    public GuardedAsyncRelayCommand CommitCommand { get; }
    public IRelayCommand CloseCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public bool HasContext => !_disposed && _context is not null;
    public bool IsBusy => _busy;
    public bool IsEditorEnabled => !_disposed && !IsBusy;
    public bool IsOpen { get => _open; private set => SetProperty(ref _open, value); }
    public bool HasPreview => _preview is not null;
    public bool HasStatus => Status.Length > 0;
    public bool CanCancel => !_disposed && _operation is { IsCancellationRequested: false };
    private bool CanStart => HasContext && !IsBusy && _context!.ToolsEnabled && _context.ApprovalPolicy != ApprovalPolicy.ReadOnly;
    public bool CanCommit => CanStart && IsOpen && _preview is { StagedFiles: > 0 }
        && !string.IsNullOrWhiteSpace(Message) && Message.Length <= 8192
        && !Message.Any(character => char.IsControl(character) && character is not ('\r' or '\n' or '\t'));
    public string Message { get => _message; set { if (SetProperty(ref _message, value)) { OnPropertyChanged(nameof(CanCommit)); CommitCommand.NotifyCanExecuteChanged(); } } }
    public string Status { get => _status; private set { if (SetProperty(ref _status, value)) OnPropertyChanged(nameof(HasStatus)); } }
    public string PreviewSummary => _preview is { } preview ? InspectorText.Format("GitCommitPreviewCount", preview.StagedFiles) : string.Empty;
    public string BranchSummary => _preview is { } preview ? InspectorText.Format("GitCommitBranch",
        preview.Branch is { } branch ? branch["refs/heads/".Length..] : InspectorText.Get("GitCommitDetached"),
        preview.Head is { } head ? head[..Math.Min(12, head.Length)] : InspectorText.Get("GitCommitInitial")) : string.Empty;

    public void Attach(ToolExecutionContext? context)
    {
        if (_disposed) return;
        if (_context is { } previous) _drafts[previous.TaskId] = Message;
        _generation++;
        _selection.Cancel();
        _selection.Dispose();
        _selection = new();
        _context = context;
        _preview = null;
        Message = context is null ? string.Empty : _drafts.GetValueOrDefault(context.TaskId, string.Empty);
        IsOpen = false;
        Status = string.Empty;
        NotifyState();
    }

    private async Task OpenAsync(CancellationToken token)
    {
        if (!CanStart) return;
        IsOpen = true;
        _preview = null;
        await RunAsync(false, token);
    }

    private async Task RunAsync(bool commit, CancellationToken caller)
    {
        if (!CanStart || !IsOpen || commit && !CanCommit) return;
        var context = _context! with { AuthorizedRoots = _context!.AuthorizedRoots.ToArray() };
        var generation = _generation;
        var message = Message;
        var expected = _preview?.State;
        var closeAfterCompletion = false;
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(caller, deadline.Token, _selection.Token, _shutdown.Token);
        _operation = operation;
        _busy = true;
        Status = InspectorText.Get(commit ? "GitCommitRunning" : "GitCommitLoading");
        NotifyState();
        try
        {
            var arguments = commit ? JsonSerializer.SerializeToElement(new { message, expectedState = expected }) : JsonSerializer.SerializeToElement(new { });
            var result = await _tools.ExecuteAsync(new(Guid.NewGuid().ToString("N"), commit ? "git.commit" : "git.commit_preview", arguments), context, operation.Token).ConfigureAwait(false);
            var preview = !commit && result.IsSuccess && !operation.IsCancellationRequested ? ParsePreview(result.Content) : null;
            if (commit && result.IsSuccess && IsCurrent(generation) && !operation.IsCancellationRequested)
                await RefreshAsync(generation, operation.Token).ConfigureAwait(false);
            await PublishAsync(() =>
            {
                if (!IsCurrent(generation)) return;
                _preview = !operation.IsCancellationRequested && result.IsSuccess ? preview : null;
                Status = InspectorText.Get(operation.IsCancellationRequested ? "GitCommitInterrupted"
                    : !result.IsSuccess ? FailureKey(result.ErrorCode) : commit ? "GitCommitSucceeded"
                    : preview!.StagedFiles == 0 ? "GitCommitNothingStaged" : "GitCommitReviewHint");
                if (commit && result.IsSuccess && !operation.IsCancellationRequested)
                {
                    if (Message == message) Message = string.Empty;
                    closeAfterCompletion = true;
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        { await PublishFailureAsync(generation, "GitCommitInterrupted").ConfigureAwait(false); }
        catch (Exception)
        { await PublishFailureAsync(generation, "GitCommitFailed").ConfigureAwait(false); }
        finally
        {
            _operation = null;
            await PublishAsync(() =>
            {
                _busy = false;
                if (closeAfterCompletion && IsCurrent(generation)) IsOpen = false;
                NotifyState();
            }).ConfigureAwait(false);
        }
    }

    private static GitCommitPreview ParsePreview(string? content)
    {
        var preview = JsonSerializer.Deserialize<GitCommitPreview>(content ?? string.Empty, JsonSerializerOptions.Web);
        if (preview is null || preview.State.Length != 64 || !preview.State.All(char.IsAsciiHexDigit) || preview.StagedFiles < 0
            || preview.Branch is { } branch && !branch.StartsWith("refs/heads/", StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid Git commit preview.");
        return preview;
    }

    private async Task RefreshAsync(int generation, CancellationToken token)
    {
        Task? work = null;
        await AvaloniaUiThread.RunAsync(() => { if (IsCurrent(generation) && !token.IsCancellationRequested) work = _refresh(token); }, token).ConfigureAwait(false);
        if (work is not null) await work.ConfigureAwait(false);
    }

    private Task PublishFailureAsync(int generation, string key) => PublishAsync(() =>
    { if (IsCurrent(generation)) { _preview = null; Status = InspectorText.Get(key); } });

    private static string FailureKey(string? error) => error switch
    {
        "git_state_changed" => "GitCommitStateChanged",
        "git_nothing_staged" => "GitCommitNothingStaged",
        "git_unmerged" => "GitCommitConflicts",
        "approval_denied" or "capability_denied" => "DiffIndexDenied",
        "path_denied" or "workspace_denied" => "DiffIndexPathDenied",
        "tool_cancelled" or "tool_timeout" => "GitCommitInterrupted",
        _ => "GitCommitFailed"
    };

    private bool IsCurrent(int generation) => !_disposed && generation == _generation;

    private async Task PublishAsync(Action publish)
    {
        if (_disposed) return;
        try { await AvaloniaUiThread.RunAsync(() => { if (!_disposed) publish(); }, _shutdown.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
    }

    private void Cancel()
    {
        if (!CanCancel) return;
        _operation!.Cancel();
        Status = InspectorText.Get("GitCommitCancelling");
        NotifyState();
    }

    private void NotifyState()
    {
        foreach (var name in new[] { nameof(HasContext), nameof(IsBusy), nameof(IsEditorEnabled), nameof(HasPreview), nameof(CanCommit), nameof(CanCancel), nameof(PreviewSummary), nameof(BranchSummary) }) OnPropertyChanged(name);
        OpenCommand.NotifyCanExecuteChanged();
        RefreshPreviewCommand.NotifyCanExecuteChanged();
        CommitCommand.NotifyCanExecuteChanged();
        CloseCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _generation++;
        _shutdown.Cancel();
        _selection.Cancel();
        OpenCommand.Dispose();
        RefreshPreviewCommand.Dispose();
        CommitCommand.Dispose();
        _drafts.Clear();
        NotifyState();
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await Task.WhenAll(new[] { OpenCommand.ExecutionTask, RefreshPreviewCommand.ExecutionTask, CommitCommand.ExecutionTask }.OfType<Task>()).ConfigureAwait(false);
        _shutdown.Dispose();
        _selection.Dispose();
    }
}
