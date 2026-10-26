using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.App.Resources;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class PullRequestCloseViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IToolExecutor _tools;
    private readonly Func<Task> _reload;
    private CancellationTokenSource? _operation;
    private ToolExecutionContext? _context;
    private PullRequestRevision? _revision;
    private string _status = string.Empty;
    private bool _busy;
    private bool _confirming;
    private bool _disposed;
    private bool _isOpen;
    private int _generation;

    public PullRequestCloseViewModel(IToolExecutor tools, Func<Task> reload)
    {
        _tools = tools;
        _reload = reload;
        CloseCommand = new(token => CloseAsync(token), CanCloseNow);
        CancelCommand = new RelayCommand(() => _operation?.Cancel(), () => _busy);
    }

    public string Status { get => _status; private set { SetProperty(ref _status, value); OnPropertyChanged(nameof(HasStatus)); } }
    public bool HasStatus => Status.Length > 0;
    public bool IsBusy => _busy;
    public bool IsActive => _revision is not null;
    public bool IsOpen => IsActive && _isOpen;
    public bool CanWrite => IsActive && _context?.SourceScope?.CanWrite == true;
    public bool CanClose => CanWrite && _isOpen;
    public bool ShowReadOnlyHint => IsOpen && !CanWrite;
    public string CloseLabel => _confirming ? PullRequestText.CloseConfirm : PullRequestText.ClosePullRequest;
    public GuardedAsyncRelayCommand CloseCommand { get; }
    public IRelayCommand CancelCommand { get; }

    public void Attach(ToolExecutionContext context, PullRequestRevision revision, bool isOpen)
    {
        Clear();
        _context = context;
        _revision = revision;
        _isOpen = isOpen;
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(IsOpen));
        OnPropertyChanged(nameof(CanWrite));
        OnPropertyChanged(nameof(CanClose));
        OnPropertyChanged(nameof(ShowReadOnlyHint));
    }

    public void Clear()
    {
        _generation++;
        _operation?.Cancel();
        _context = null;
        _revision = null;
        _isOpen = false;
        _busy = false;
        _confirming = false;
        Status = string.Empty;
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(IsOpen));
        OnPropertyChanged(nameof(CanWrite));
        OnPropertyChanged(nameof(CanClose));
        OnPropertyChanged(nameof(ShowReadOnlyHint));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CloseLabel));
        CloseCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    private bool CanCloseNow() => !_disposed && !_busy && CanClose;

    private async Task CloseAsync(CancellationToken caller)
    {
        if (!CanCloseNow()) return;
        if (!_confirming)
        {
            _confirming = true;
            Status = PullRequestText.CloseConfirmHint;
            OnPropertyChanged(nameof(CloseLabel));
            CloseCommand.NotifyCanExecuteChanged();
            return;
        }
        var generation = _generation;
        var context = _context!;
        var revision = _revision!;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(caller);
        operation.CancelAfter(TimeSpan.FromSeconds(90));
        _operation = operation;
        _busy = true;
        _confirming = false;
        Status = PullRequestText.CloseSubmitting;
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CloseLabel));
        CloseCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        try
        {
            var result = await Task.Run(() => _tools.ExecuteAsync(Request(revision), WriteContext(context),
                operation.Token), operation.Token).ConfigureAwait(false);
            var reconciled = Reconciled(result);
            await PublishAsync(() =>
            {
                if (generation != _generation) return;
                if (operation.IsCancellationRequested)
                {
                    Status = PullRequestText.CloseCancelled;
                    return;
                }
                if (!result.IsSuccess)
                {
                    Status = PullRequestText.Failure(result.ErrorCode ?? "source_unavailable", "close");
                    return;
                }
                Status = PullRequestText.Get(reconciled ? "ClosedReconciled" : "Closed");
            }).ConfigureAwait(false);
            if (result.IsSuccess && generation == _generation) _ = _reload();
        }
        catch (OperationCanceledException)
        { await PublishAsync(() => { if (generation == _generation) Status = PullRequestText.CloseCancelled; }).ConfigureAwait(false); }
        catch (Exception)
        { await PublishAsync(() => { if (generation == _generation) Status = PullRequestText.Get("Failed"); }).ConfigureAwait(false); }
        finally
        {
            _operation = null;
            await PublishAsync(() =>
            {
                _busy = false;
                OnPropertyChanged(nameof(IsBusy));
                CloseCommand.NotifyCanExecuteChanged();
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

    private static ToolRequest Request(PullRequestRevision revision) =>
        new(Guid.NewGuid().ToString("N"), "source.pr_close", JsonSerializer.SerializeToElement(
            new Dictionary<string, object>
            {
                ["number"] = revision.Number,
                ["base_revision"] = revision.BaseRevision,
                ["head_revision"] = revision.HeadRevision
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
        if (CloseCommand.ExecutionTask is { } work) await work.ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Clear();
        CloseCommand.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await DrainAsync().ConfigureAwait(false);
    }
}
