using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.App.Resources;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;

namespace MossAgent.App.ViewModels;

public sealed class GitPushViewModel : ObservableObject, IDisposable, IAsyncDisposable
{
    private readonly IToolExecutor _tools;
    private readonly Func<CancellationToken, Task> _refresh;
    private readonly Dictionary<Guid, (string Remote, string Branch)> _selections = [];
    private readonly CancellationTokenSource _shutdown = new();
    private CancellationTokenSource _selection = new();
    private CancellationTokenSource? _operation;
    private ToolExecutionContext? _context;
    private GitPushPreview? _preview;
    private string _remote = string.Empty;
    private string _branch = string.Empty;
    private string _remotes = string.Empty;
    private string _status = string.Empty;
    private bool _open;
    private bool _busy;
    private bool _disposed;
    private int _generation;
    private int _inputRevision;

    public GitPushViewModel(IToolExecutor tools, Func<CancellationToken, Task> refresh)
    {
        _tools = tools;
        _refresh = refresh;
        OpenCommand = new(OpenAsync, () => CanStart && !IsOpen);
        RefreshPreviewCommand = new(token => RunAsync(false, token), () => CanStart && IsOpen);
        PushCommand = new(token => RunAsync(true, token), () => CanPush);
        CloseCommand = new RelayCommand(() => { IsOpen = false; NotifyState(); }, () => !_disposed && IsOpen && !IsBusy);
        CancelCommand = new RelayCommand(() => { _operation?.Cancel(); Status = InspectorText.Get("GitCommitCancelling"); NotifyState(); }, () => CanCancel);
    }

    public GuardedAsyncRelayCommand OpenCommand { get; }
    public GuardedAsyncRelayCommand RefreshPreviewCommand { get; }
    public GuardedAsyncRelayCommand PushCommand { get; }
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
    public bool CanPush => CanStart && IsOpen && _preview is { } preview && preview.Remote == Remote && preview.TargetBranch == TargetBranch;
    public string Remote { get => _remote; set => Edit(ref _remote, value, nameof(Remote)); }
    public string TargetBranch { get => _branch; set => Edit(ref _branch, value, nameof(TargetBranch)); }
    public string AvailableRemotes => _remotes;
    public string Status { get => _status; private set { if (SetProperty(ref _status, value)) OnPropertyChanged(nameof(HasStatus)); } }
    public string PreviewSummary => _preview is { } preview ? InspectorText.Format("GitPushSummary", preview.Head[..Math.Min(12, preview.Head.Length)],
        preview.Remote, preview.TargetBranch, preview.Destination) : string.Empty;
    public bool HasCredential => _preview is not null;
    public bool HasCredentialWarning => _preview?.CredentialMethod == GitRemoteCredentialMethod.Embedded;
    public string CredentialSummary => _preview is { } preview
        ? InspectorText.Format("GitPushCredential", InspectorText.Get(MethodKey(preview.CredentialMethod)))
            + (preview.CredentialMethod == GitRemoteCredentialMethod.Embedded ? " " + InspectorText.Get("GitPushCredentialEmbedded") : string.Empty)
        : string.Empty;

    private static string MethodKey(GitRemoteCredentialMethod method) => method switch
    {
        GitRemoteCredentialMethod.Local => "GitPushCredentialMethodLocal",
        GitRemoteCredentialMethod.Ssh => "GitPushCredentialMethodSsh",
        GitRemoteCredentialMethod.Https => "GitPushCredentialMethodHttps",
        GitRemoteCredentialMethod.Embedded => "GitPushCredentialMethodEmbedded",
        _ => "GitPushCredentialMethodUnsupported"
    };

    private void Edit(ref string field, string? value, string property)
    {
        value ??= string.Empty;
        if (field == value) return;
        field = value;
        _inputRevision++;
        _preview = null;
        OnPropertyChanged(property);
        if (IsOpen && !IsBusy) Status = InspectorText.Get("GitPushSelectionChanged");
        NotifyState();
    }

    public void Attach(ToolExecutionContext? context)
    {
        if (_disposed) return;
        if (_context is { } previous) _selections[previous.TaskId] = (Remote, TargetBranch);
        _generation++;
        _selection.Cancel();
        _selection.Dispose();
        _selection = new();
        _context = context;
        var saved = context is null ? default : _selections.GetValueOrDefault(context.TaskId);
        _remote = saved.Remote ?? string.Empty;
        _branch = saved.Branch ?? string.Empty;
        _remotes = string.Empty;
        _preview = null;
        IsOpen = false;
        Status = string.Empty;
        foreach (var property in new[] { nameof(Remote), nameof(TargetBranch), nameof(AvailableRemotes) }) OnPropertyChanged(property);
        NotifyState();
    }

    private async Task OpenAsync(CancellationToken token)
    {
        if (!CanStart) return;
        IsOpen = true;
        await RunAsync(false, token);
    }

    private async Task RunAsync(bool push, CancellationToken caller)
    {
        if (!CanStart || !IsOpen || push && !CanPush) return;
        var context = _context! with { AuthorizedRoots = _context!.AuthorizedRoots.ToArray() };
        var generation = _generation;
        var revision = _inputRevision;
        var expectedHead = _preview?.Head;
        var arguments = Arguments(push);
        var close = false;
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(caller, deadline.Token, _selection.Token, _shutdown.Token);
        _operation = operation;
        _busy = true;
        Status = InspectorText.Get(push ? "GitPushRunning" : "GitPushLoading");
        NotifyState();
        try
        {
            var result = await _tools.ExecuteAsync(new(Guid.NewGuid().ToString("N"), push ? "git.push" : "git.push_preview", arguments), context, operation.Token).ConfigureAwait(false);
            // After a non-deterministic failure the push may have landed anyway; a successful push
            // refreshes refs/remotes/<remote>/<target> locally, so re-reading the preview with the
            // same operation token proves the outcome without touching network or credentials.
            var verification = GitPushVerification.None;
            if (push && !result.IsSuccess && !operation.IsCancellationRequested
                && !GitPushOutcomeClassifier.IsDeterministic(result.ErrorCode))
                verification = await VerifyPushAsync(context, arguments, expectedHead, operation.Token).ConfigureAwait(false);
            var preview = !push && result.IsSuccess && !operation.IsCancellationRequested ? Parse(result.Content) : null;
            if (push && result.IsSuccess && IsCurrent(generation) && !operation.IsCancellationRequested)
            {
                Task? refresh = null;
                await AvaloniaUiThread.RunAsync(() => { if (IsCurrent(generation)) refresh = _refresh(operation.Token); }, operation.Token).ConfigureAwait(false);
                if (refresh is not null) await refresh.ConfigureAwait(false);
            }
            await PublishAsync(() =>
            {
                if (!IsCurrent(generation)) return;
                _preview = null;
                Status = InspectorText.Get(StatusKey(push, result, verification, revision != _inputRevision, operation.IsCancellationRequested));
                if (!push && preview is not null && !operation.IsCancellationRequested && revision == _inputRevision)
                {
                    _remote = preview.Remote;
                    _branch = preview.TargetBranch;
                    _remotes = InspectorText.Format("GitPushRemotes", string.Join("、", preview.Remotes));
                    _preview = preview;
                    foreach (var property in new[] { nameof(Remote), nameof(TargetBranch), nameof(AvailableRemotes) }) OnPropertyChanged(property);
                }
                close = push && result.IsSuccess && !operation.IsCancellationRequested && revision == _inputRevision;
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { await FailureAsync(generation, "GitPushInterrupted").ConfigureAwait(false); }
        catch (Exception) { await FailureAsync(generation, "GitPushFailed").ConfigureAwait(false); }
        finally
        {
            _operation = null;
            await PublishAsync(() => { _busy = false; if (close && IsCurrent(generation)) IsOpen = false; NotifyState(); }).ConfigureAwait(false);
        }
    }

    private JsonElement Arguments(bool push)
    {
        var values = new Dictionary<string, string>();
        if (Remote.Length > 0) values["remote"] = Remote;
        if (TargetBranch.Length > 0) values["targetBranch"] = TargetBranch;
        if (push) values["expectedState"] = _preview!.State;
        return JsonSerializer.SerializeToElement(values);
    }

    private async Task<GitPushVerification> VerifyPushAsync(ToolExecutionContext context, JsonElement pushArguments,
        string? expectedHead, CancellationToken token)
    {
        try
        {
            var selection = new Dictionary<string, string>();
            if (pushArguments.TryGetProperty("remote", out var remote)) selection["remote"] = remote.GetString()!;
            if (pushArguments.TryGetProperty("targetBranch", out var target)) selection["targetBranch"] = target.GetString()!;
            var result = await _tools.ExecuteAsync(new(Guid.NewGuid().ToString("N"), "git.push_preview",
                JsonSerializer.SerializeToElement(selection)), context, token).ConfigureAwait(false);
            if (!result.IsSuccess) return GitPushVerification.Unknown;
            var preview = Parse(result.Content);
            return expectedHead is { } head && string.Equals(preview.RemoteHead, head, StringComparison.OrdinalIgnoreCase)
                ? GitPushVerification.Landed : GitPushVerification.Unproven;
        }
        catch (Exception) { return GitPushVerification.Unknown; }
    }

    private static GitPushPreview Parse(string? content)
    {
        var preview = JsonSerializer.Deserialize<GitPushPreview>(content ?? string.Empty, JsonSerializerOptions.Web);
        if (preview is null || preview.State.Length != 64 || !preview.State.All(char.IsAsciiHexDigit)
            || preview.Head.Length is not (40 or 64) || !preview.Head.All(char.IsAsciiHexDigit)
            || !GitPushNames.IsRemote(preview.Remote) || !GitPushNames.IsBranch(preview.TargetBranch)
            || preview.Remotes is null || preview.Remotes.Count is 0 or > 128 || preview.Remotes.Any(remote => !GitPushNames.IsRemote(remote))
            || preview.Destination.Length is 0 or > 2048 || preview.Destination.Any(char.IsControl)
            || preview.RemoteHead is { } remoteHead && (remoteHead.Length is not (40 or 64) || !remoteHead.All(char.IsAsciiHexDigit)))
            throw new InvalidOperationException("Invalid push preview.");
        return preview;
    }

    private static string StatusKey(bool push, ToolResult result, GitPushVerification verification, bool changed, bool cancelled)
    {
        if (verification == GitPushVerification.Landed) return "GitPushProbablyLanded";
        if (cancelled) return "GitPushInterrupted";
        if (verification == GitPushVerification.Unknown) return "GitPushOutcomeUnknown";
        if (!result.IsSuccess) return FailureKey(result.ErrorCode);
        if (changed) return push ? "GitPushChangedAfterSuccess" : "GitPushSelectionChanged";
        return push ? "GitPushSucceeded" : "GitPushReview";
    }

    private static string FailureKey(string? code) => code switch
    {
        "git_state_changed" => "GitPushStateChanged",
        "git_push_no_remote" => "GitPushNoRemote",
        "git_push_no_head" => "GitPushNoHead",
        "git_push_branch" => "GitPushBranchRequired",
        "git_push_transport" => "GitPushTransportUnsupported",
        "git_push_multiple_urls" => "GitPushMultipleUrls",
        "git_push_non_ff" => "GitPushNonFastForward",
        "approval_denied" or "capability_denied" => "DiffIndexDenied",
        "path_denied" or "workspace_denied" => "DiffIndexPathDenied",
        "tool_cancelled" or "tool_timeout" => "GitPushInterrupted",
        _ => "GitPushFailed"
    };
    private bool IsCurrent(int generation) => !_disposed && generation == _generation;
    private Task FailureAsync(int generation, string key) => PublishAsync(() => { if (IsCurrent(generation)) { _preview = null; Status = InspectorText.Get(key); } });
    private async Task PublishAsync(Action publish)
    {
        if (_disposed) return;
        try { await AvaloniaUiThread.RunAsync(() => { if (!_disposed) publish(); }, _shutdown.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
    }

    private void NotifyState()
    {
        foreach (var property in new[] { nameof(HasContext), nameof(IsBusy), nameof(IsEditorEnabled), nameof(HasPreview), nameof(CanPush), nameof(CanCancel), nameof(PreviewSummary), nameof(HasCredential), nameof(HasCredentialWarning), nameof(CredentialSummary) }) OnPropertyChanged(property);
        OpenCommand.NotifyCanExecuteChanged();
        RefreshPreviewCommand.NotifyCanExecuteChanged();
        PushCommand.NotifyCanExecuteChanged();
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
        PushCommand.Dispose();
        _selections.Clear();
        NotifyState();
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await Task.WhenAll(new[] { OpenCommand.ExecutionTask, RefreshPreviewCommand.ExecutionTask, PushCommand.ExecutionTask }.OfType<Task>()).ConfigureAwait(false);
        _shutdown.Dispose();
        _selection.Dispose();
    }
}
