using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.App.Resources;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.SourceControl;

namespace MossAgent.App.ViewModels;

public sealed class PullRequestCreateViewModel : ObservableObject, IAsyncDisposable
{
    private const int MaximumTitleLength = 256;
    private const int MaximumBodyLength = 65536;
    private readonly IToolExecutor _tools;
    private readonly ISourcePullRequestBranches _branches;
    private readonly Func<SourceRepositoryScope, CancellationToken, Task<ToolExecutionContext>> _context;
    private readonly Func<SourceRepositoryScope, PullRequestCreateReceipt, CancellationToken, Task> _created;
    private readonly CancellationTokenSource _shutdown = new();
    private CancellationTokenSource? _operation;
    private SourceRepositoryScope? _scope;
    private ToolExecutionContext? _writeContext;
    private PullRequestBranch? _base;
    private PullRequestBranch? _head;
    private string _title = string.Empty;
    private string _body = string.Empty;
    private string _status = string.Empty;
    private bool _draft;
    private bool _busy;
    private bool _confirming;
    private bool _disposed;
    private bool _isOpen;
    private int _generation;

    public PullRequestCreateViewModel(IToolExecutor tools, ISourcePullRequestBranches branches,
        Func<SourceRepositoryScope, CancellationToken, Task<ToolExecutionContext>> context,
        Func<SourceRepositoryScope, PullRequestCreateReceipt, CancellationToken, Task> created)
    {
        _tools = tools;
        _branches = branches;
        _context = context;
        _created = created;
        SubmitCommand = new(token => SubmitAsync(token), CanSubmitNow);
        CancelCommand = new RelayCommand(() => _operation?.Cancel(), () => _busy);
        CloseCommand = new RelayCommand(Close, () => !_disposed && IsOpen);
    }

    public ObservableCollection<PullRequestBranch> Branches { get; } = [];
    public GuardedAsyncRelayCommand SubmitCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand CloseCommand { get; }
    public string Status { get => _status; private set { SetProperty(ref _status, value); OnPropertyChanged(nameof(HasStatus)); } }
    public bool HasStatus => Status.Length > 0;
    public bool IsBusy => _busy;
    public bool IsDisposed => _disposed;
    public bool IsOpen { get => _isOpen; private set { SetProperty(ref _isOpen, value); NotifyCommands(); } }
    public string SubmitLabel => _confirming ? PullRequestText.CreatePrConfirm : PullRequestText.CreatePullRequest;

    public PullRequestBranch? SelectedBase
    {
        get => _base;
        set { if (SetProperty(ref _base, value)) InputChanged(); }
    }

    public PullRequestBranch? SelectedHead
    {
        get => _head;
        set { if (SetProperty(ref _head, value)) InputChanged(); }
    }

    public string Title
    {
        get => _title;
        set { if (SetProperty(ref _title, value)) InputChanged(); }
    }

    public string Body
    {
        get => _body;
        set { if (SetProperty(ref _body, value)) InputChanged(); }
    }

    public bool IsDraft
    {
        get => _draft;
        set { if (SetProperty(ref _draft, value)) InputChanged(); }
    }

    // Both branches must be picked explicitly — no defaulting — and head/base must
    // differ; the writer re-validates every bound before posting.
    public bool CanSubmit => IsOpen && !_busy && _scope?.CanWrite == true && _writeContext is not null
        && _base is not null && _head is not null
        && !_base.Name.Equals(_head.Name, StringComparison.OrdinalIgnoreCase)
        && Title.Trim() is { Length: > 0 and <= MaximumTitleLength } && Body.Length <= MaximumBodyLength;

    public async Task OpenAsync(SourceRepositoryScope scope, CancellationToken caller)
    {
        if (_disposed || _busy || IsOpen) return;
        Reset();
        var generation = _generation;
        _scope = scope;
        IsOpen = true;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(caller, _shutdown.Token);
        operation.CancelAfter(TimeSpan.FromSeconds(40));
        _operation = operation;
        _busy = true;
        Status = PullRequestText.BranchesLoading;
        NotifyCommands();
        try
        {
            var branches = await Task.Run(() => _branches.ListAsync(scope, operation.Token),
                operation.Token).ConfigureAwait(false);
            var context = await _context(scope, operation.Token).ConfigureAwait(false);
            await PublishAsync(() =>
            {
                if (generation != _generation || operation.IsCancellationRequested || !IsOpen) return;
                foreach (var branch in branches) Branches.Add(branch);
                _writeContext = context;
                Status = string.Empty;
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        { await PublishAsync(() => { if (generation == _generation && IsOpen) Status = PullRequestText.Get("Cancelled"); }).ConfigureAwait(false); }
        catch (Exception)
        { await PublishAsync(() => { if (generation == _generation && IsOpen) Status = PullRequestText.BranchesFailed; }).ConfigureAwait(false); }
        finally
        {
            _operation = null;
            await PublishAsync(() => { _busy = false; NotifyCommands(); }).ConfigureAwait(false);
        }
    }

    private bool CanSubmitNow() => !_disposed && CanSubmit;

    private void InputChanged()
    {
        _confirming = false;
        OnPropertyChanged(nameof(SubmitLabel));
        SubmitCommand.NotifyCanExecuteChanged();
    }

    private void NotifyCommands()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(SubmitLabel));
        SubmitCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        CloseCommand.NotifyCanExecuteChanged();
    }

    private void Reset()
    {
        _generation++;
        _operation?.Cancel();
        _writeContext = null;
        _base = null; _head = null;
        _title = string.Empty; _body = string.Empty;
        _draft = false; _confirming = false; _busy = false;
        Branches.Clear();
        Status = string.Empty;
        OnPropertyChanged(nameof(SelectedBase));
        OnPropertyChanged(nameof(SelectedHead));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Body));
        OnPropertyChanged(nameof(IsDraft));
    }

    public void Close()
    {
        if (!IsOpen) return;
        _operation?.Cancel();
        _scope = null;
        IsOpen = false;
        Reset();
        NotifyCommands();
    }

    private async Task SubmitAsync(CancellationToken caller)
    {
        if (!CanSubmitNow()) return;
        if (!_confirming)
        {
            _confirming = true;
            Status = PullRequestText.CreatePrConfirmHint;
            OnPropertyChanged(nameof(SubmitLabel));
            SubmitCommand.NotifyCanExecuteChanged();
            return;
        }
        var generation = _generation;
        var scope = _scope!;
        var context = _writeContext!;
        var request = Request(Title.Trim(), string.IsNullOrWhiteSpace(Body) ? null : Body,
            SelectedHead!.Name, SelectedBase!.Name, IsDraft);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(caller, _shutdown.Token);
        operation.CancelAfter(TimeSpan.FromSeconds(90));
        _operation = operation;
        _busy = true;
        _confirming = false;
        Status = PullRequestText.CreatePrSubmitting;
        NotifyCommands();
        try
        {
            var result = await Task.Run(() => _tools.ExecuteAsync(request, WriteContext(context),
                operation.Token), operation.Token).ConfigureAwait(false);
            var receipt = Receipt(result);
            await PublishAsync(() =>
            {
                if (generation != _generation) return;
                if (operation.IsCancellationRequested) { Status = PullRequestText.CreatePrCancelled; return; }
                if (receipt is null) { Status = PullRequestText.Failure(result.ErrorCode ?? "source_unavailable", "create"); return; }
                IsOpen = false;
            }).ConfigureAwait(false);
            if (receipt is not null && generation == _generation && !operation.IsCancellationRequested)
                await _created(scope, receipt, operation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        { await PublishAsync(() => { if (generation == _generation && IsOpen) Status = PullRequestText.CreatePrCancelled; }).ConfigureAwait(false); }
        catch (SourceReviewException exception)
        { await PublishAsync(() => { if (generation == _generation) Status = PullRequestText.Failure(exception.ErrorCode, "create"); }).ConfigureAwait(false); }
        catch (Exception)
        { await PublishAsync(() => { if (generation == _generation) Status = PullRequestText.Get("Failed"); }).ConfigureAwait(false); }
        finally
        {
            _operation = null;
            await PublishAsync(() =>
            {
                _busy = false;
                if (generation == _generation && !IsOpen) { _scope = null; _writeContext = null; Branches.Clear(); }
                NotifyCommands();
            }).ConfigureAwait(false);
        }
    }

    // The two-tap confirm above is the user's explicit write approval; the pipeline still
    // schema-validates and write-resolves the live grant, so FullAccess here only lifts
    // the read-only capability gate for this one call. The 80s deadline leaves room for
    // the transport timeout plus a single reconciliation read.
    private static ToolExecutionContext WriteContext(ToolExecutionContext context) =>
        context with { ApprovalPolicy = ApprovalPolicy.FullAccess, Timeout = TimeSpan.FromSeconds(80) };

    private static ToolRequest Request(string title, string? body, string head, string baseName, bool draft)
    {
        var arguments = new Dictionary<string, object>
        { ["title"] = title, ["head"] = head, ["base"] = baseName, ["draft"] = draft };
        if (body is not null) arguments["body"] = body;
        return new(Guid.NewGuid().ToString("N"), "source.pr_create", JsonSerializer.SerializeToElement(arguments));
    }

    private static PullRequestCreateReceipt? Receipt(ToolResult result)
    {
        if (!result.IsSuccess || result.Content is null) return null;
        try { return JsonSerializer.Deserialize<PullRequestCreateReceipt>(result.Content); }
        catch (Exception) { return null; }
    }

    private Task PublishAsync(Action action) =>
        AvaloniaUiThread.RunAsync(() => { if (!_disposed) action(); }, CancellationToken.None);

    public async Task DrainAsync()
    {
        if (SubmitCommand.ExecutionTask is { } work) await work.ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _shutdown.Cancel();
        Close();
        SubmitCommand.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await DrainAsync().ConfigureAwait(false);
        _shutdown.Dispose();
    }
}
