using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.App.Resources;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class PullRequestMergeViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IToolExecutor _tools;
    private readonly Func<Task> _reload;
    private CancellationTokenSource? _operation;
    private ToolExecutionContext? _context;
    private PullRequestRevision? _revision;
    private PullRequestMergeMethodOption? _method;
    private string _status = string.Empty;
    private bool _busy;
    private bool _confirming;
    private bool _disposed;
    private bool _isOpen;
    private bool _isDraft;
    private int _generation;

    public PullRequestMergeViewModel(IToolExecutor tools, Func<Task> reload)
    {
        _tools = tools;
        _reload = reload;
        Methods =
        [
            new(PullRequestMergeMethod.Merge, "merge", PullRequestText.Get("MergeMethod_Merge")),
            new(PullRequestMergeMethod.Squash, "squash", PullRequestText.Get("MergeMethod_Squash")),
            new(PullRequestMergeMethod.Rebase, "rebase", PullRequestText.Get("MergeMethod_Rebase"))
        ];
        MergeCommand = new(token => MergeAsync(token), CanMergeNow);
        CancelCommand = new RelayCommand(() => _operation?.Cancel(), () => _busy);
    }

    public IReadOnlyList<PullRequestMergeMethodOption> Methods { get; }

    public PullRequestMergeMethodOption? SelectedMethod
    {
        get => _method;
        set
        {
            if (!SetProperty(ref _method, value)) return;
            _confirming = false;
            OnPropertyChanged(nameof(MergeLabel));
            MergeCommand.NotifyCanExecuteChanged();
        }
    }

    public string Status { get => _status; private set { SetProperty(ref _status, value); OnPropertyChanged(nameof(HasStatus)); } }
    public bool HasStatus => Status.Length > 0;
    public bool IsBusy => _busy;
    public bool IsActive => _revision is not null;
    public bool IsOpen => IsActive && _isOpen;
    public bool IsDraft => IsActive && _isDraft;
    public bool CanWrite => IsActive && _context?.SourceScope?.CanWrite == true;
    public bool CanMerge => CanWrite && _isOpen && !_isDraft;
    public bool ShowReadOnlyHint => IsOpen && !IsDraft && !CanWrite;
    public bool ShowDraftHint => IsOpen && IsDraft;
    public string MergeLabel => _confirming ? PullRequestText.MergeConfirm : PullRequestText.MergePullRequest;
    public GuardedAsyncRelayCommand MergeCommand { get; }
    public IRelayCommand CancelCommand { get; }

    public void Attach(ToolExecutionContext context, PullRequestRevision revision, bool isOpen, bool isDraft)
    {
        Clear();
        _context = context;
        _revision = revision;
        _isOpen = isOpen;
        _isDraft = isDraft;
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(IsOpen));
        OnPropertyChanged(nameof(IsDraft));
        OnPropertyChanged(nameof(CanWrite));
        OnPropertyChanged(nameof(CanMerge));
        OnPropertyChanged(nameof(ShowReadOnlyHint));
        OnPropertyChanged(nameof(ShowDraftHint));
    }

    public void Clear()
    {
        _generation++;
        _operation?.Cancel();
        _context = null;
        _revision = null;
        _isOpen = false;
        _isDraft = false;
        _busy = false;
        _confirming = false;
        _method = null;
        Status = string.Empty;
        OnPropertyChanged(nameof(SelectedMethod));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(IsOpen));
        OnPropertyChanged(nameof(IsDraft));
        OnPropertyChanged(nameof(CanWrite));
        OnPropertyChanged(nameof(CanMerge));
        OnPropertyChanged(nameof(ShowReadOnlyHint));
        OnPropertyChanged(nameof(ShowDraftHint));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(MergeLabel));
        MergeCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    // Merging requires three independent gates — an attached live revision, a write
    // grant, and an open non-draft pull request — plus an explicit method choice so
    // the strategy is never silently defaulted.
    private bool CanMergeNow() => !_disposed && !_busy && CanMerge && SelectedMethod is not null;

    private async Task MergeAsync(CancellationToken caller)
    {
        if (!CanMergeNow()) return;
        if (!_confirming)
        {
            _confirming = true;
            Status = PullRequestText.MergeConfirmHint;
            OnPropertyChanged(nameof(MergeLabel));
            MergeCommand.NotifyCanExecuteChanged();
            return;
        }
        var generation = _generation;
        var context = _context!;
        var revision = _revision!;
        var method = SelectedMethod!;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(caller);
        operation.CancelAfter(TimeSpan.FromSeconds(90));
        _operation = operation;
        _busy = true;
        _confirming = false;
        Status = PullRequestText.MergeSubmitting;
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(MergeLabel));
        MergeCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        try
        {
            var result = await Task.Run(() => _tools.ExecuteAsync(Request(revision, method), WriteContext(context),
                operation.Token), operation.Token).ConfigureAwait(false);
            var reconciled = Reconciled(result);
            await PublishAsync(() =>
            {
                if (generation != _generation) return;
                if (operation.IsCancellationRequested)
                {
                    Status = PullRequestText.MergeCancelled;
                    return;
                }
                if (!result.IsSuccess)
                {
                    Status = PullRequestText.Failure(result.ErrorCode ?? "source_unavailable", "merge");
                    return;
                }
                Status = PullRequestText.Get(reconciled ? "MergedReconciled" : "Merged");
            }).ConfigureAwait(false);
            if (result.IsSuccess && generation == _generation) _ = _reload();
        }
        catch (OperationCanceledException)
        { await PublishAsync(() => { if (generation == _generation) Status = PullRequestText.MergeCancelled; }).ConfigureAwait(false); }
        catch (Exception)
        { await PublishAsync(() => { if (generation == _generation) Status = PullRequestText.Get("Failed"); }).ConfigureAwait(false); }
        finally
        {
            _operation = null;
            await PublishAsync(() =>
            {
                _busy = false;
                OnPropertyChanged(nameof(IsBusy));
                MergeCommand.NotifyCanExecuteChanged();
                CancelCommand.NotifyCanExecuteChanged();
            }).ConfigureAwait(false);
        }
    }

    // The two-tap confirm above is the user's explicit write approval; the pipeline still
    // schema-validates, write-resolves the live grant and re-checks the PR revision, so
    // FullAccess here only lifts the read-only capability gate for this one call. The 80s
    // deadline leaves room for the transport timeout plus a single reconciliation read.
    private static ToolExecutionContext WriteContext(ToolExecutionContext context) =>
        context with { ApprovalPolicy = ApprovalPolicy.FullAccess, Timeout = TimeSpan.FromSeconds(80) };

    private static ToolRequest Request(PullRequestRevision revision, PullRequestMergeMethodOption method) =>
        new(Guid.NewGuid().ToString("N"), "source.pr_merge", JsonSerializer.SerializeToElement(
            new Dictionary<string, object>
            {
                ["number"] = revision.Number,
                ["base_revision"] = revision.BaseRevision,
                ["head_revision"] = revision.HeadRevision,
                ["method"] = method.Key
            }));

    private static bool Reconciled(ToolResult result)
    {
        if (!result.IsSuccess || result.Content is null) return false;
        try
        {
            using var document = JsonDocument.Parse(result.Content);
            return document.RootElement.GetProperty("Reconciled").GetBoolean();
        }
        catch (Exception) { return false; }
    }

    private Task PublishAsync(Action action) =>
        AvaloniaUiThread.RunAsync(() => { if (!_disposed) action(); }, CancellationToken.None);

    public async Task DrainAsync()
    {
        if (MergeCommand.ExecutionTask is { } work) await work.ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Clear();
        MergeCommand.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await DrainAsync().ConfigureAwait(false);
    }
}
