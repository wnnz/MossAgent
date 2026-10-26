using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.App.Resources;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class PullRequestReviewComposerViewModel : ObservableObject, IAsyncDisposable
{
    private const int MaximumBodyLength = 4096;
    private readonly IToolExecutor _tools;
    private readonly Func<Task> _reload;
    private CancellationTokenSource? _operation;
    private ToolExecutionContext? _context;
    private PullRequestRevision? _revision;
    private PullRequestReviewDecisionOption? _decision;
    private string _text = string.Empty;
    private string _status = string.Empty;
    private bool _busy;
    private bool _confirming;
    private bool _disposed;
    private int _generation;

    public PullRequestReviewComposerViewModel(IToolExecutor tools, Func<Task> reload)
    {
        _tools = tools;
        _reload = reload;
        Decisions =
        [
            new(PullRequestReviewDecision.Approve, "approve", PullRequestText.Get("ReviewDecision_Approve")),
            new(PullRequestReviewDecision.RequestChanges, "request_changes", PullRequestText.Get("ReviewDecision_RequestChanges")),
            new(PullRequestReviewDecision.Comment, "comment", PullRequestText.Get("ReviewDecision_Comment"))
        ];
        PublishCommand = new(token => PublishAsync(token), CanPublish);
        CancelCommand = new RelayCommand(() => _operation?.Cancel(), () => _busy);
    }

    public IReadOnlyList<PullRequestReviewDecisionOption> Decisions { get; }

    public PullRequestReviewDecisionOption? SelectedDecision
    {
        get => _decision;
        set
        {
            if (!SetProperty(ref _decision, value)) return;
            _confirming = false;
            OnPropertyChanged(nameof(PublishLabel));
            PublishCommand.NotifyCanExecuteChanged();
        }
    }

    public string Text
    {
        get => _text;
        set
        {
            if (!SetProperty(ref _text, value)) return;
            _confirming = false;
            OnPropertyChanged(nameof(PublishLabel));
            PublishCommand.NotifyCanExecuteChanged();
        }
    }

    public string Status { get => _status; private set { SetProperty(ref _status, value); OnPropertyChanged(nameof(HasStatus)); } }
    public bool HasStatus => Status.Length > 0;
    public bool IsBusy => _busy;
    public bool IsActive => _revision is not null;
    public bool CanWrite => IsActive && _context?.SourceScope?.CanWrite == true;
    public string PublishLabel => _confirming ? PullRequestText.ReviewConfirm : PullRequestText.ReviewSubmit;
    public GuardedAsyncRelayCommand PublishCommand { get; }
    public IRelayCommand CancelCommand { get; }

    public void Attach(ToolExecutionContext context, PullRequestRevision revision)
    {
        Clear();
        _context = context;
        _revision = revision;
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(CanWrite));
    }

    public void Clear()
    {
        _generation++;
        _operation?.Cancel();
        _context = null;
        _revision = null;
        _busy = false;
        _confirming = false;
        _decision = null;
        _text = string.Empty;
        OnPropertyChanged(nameof(SelectedDecision));
        OnPropertyChanged(nameof(Text));
        Status = string.Empty;
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(CanWrite));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(PublishLabel));
        PublishCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    private bool CanPublish() => !_disposed && !_busy && CanWrite && SelectedDecision is not null
        && Text.Trim().Length <= MaximumBodyLength;

    private async Task PublishAsync(CancellationToken caller)
    {
        if (!CanPublish()) return;
        if (!_confirming)
        {
            _confirming = true;
            Status = PullRequestText.ReviewConfirmHint;
            OnPropertyChanged(nameof(PublishLabel));
            PublishCommand.NotifyCanExecuteChanged();
            return;
        }
        var generation = _generation;
        var context = _context!;
        var revision = _revision!;
        var decision = SelectedDecision!;
        var body = Text.Trim() is { Length: > 0 } trimmed ? trimmed : null;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(caller);
        operation.CancelAfter(TimeSpan.FromSeconds(90));
        _operation = operation;
        _busy = true;
        _confirming = false;
        Status = PullRequestText.ReviewSubmitting;
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(PublishLabel));
        PublishCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        try
        {
            var result = await Task.Run(() => _tools.ExecuteAsync(Request(revision, decision, body), WriteContext(context),
                operation.Token), operation.Token).ConfigureAwait(false);
            var reconciled = Reconciled(result);
            await PublishAsync(() =>
            {
                if (generation != _generation) return;
                if (operation.IsCancellationRequested)
                {
                    Status = PullRequestText.ReviewCancelled;
                    return;
                }
                if (!result.IsSuccess)
                {
                    Status = PullRequestText.Failure(result.ErrorCode ?? "source_unavailable", "review");
                    return;
                }
                Text = string.Empty;
                SelectedDecision = null;
                Status = PullRequestText.Get(reconciled ? "ReviewLanded" : "ReviewSubmitted");
            }).ConfigureAwait(false);
            if (result.IsSuccess && generation == _generation) _ = _reload();
        }
        catch (OperationCanceledException)
        { await PublishAsync(() => { if (generation == _generation) Status = PullRequestText.ReviewCancelled; }).ConfigureAwait(false); }
        catch (Exception)
        { await PublishAsync(() => { if (generation == _generation) Status = PullRequestText.Get("Failed"); }).ConfigureAwait(false); }
        finally
        {
            _operation = null;
            await PublishAsync(() =>
            {
                _busy = false;
                OnPropertyChanged(nameof(IsBusy));
                PublishCommand.NotifyCanExecuteChanged();
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

    private static ToolRequest Request(PullRequestRevision revision, PullRequestReviewDecisionOption decision, string? body)
    {
        var arguments = new Dictionary<string, object>
        {
            ["number"] = revision.Number,
            ["base_revision"] = revision.BaseRevision,
            ["head_revision"] = revision.HeadRevision,
            ["decision"] = decision.Key
        };
        if (body is not null) arguments["body"] = body;
        return new(Guid.NewGuid().ToString("N"), "source.pr_review_submit", JsonSerializer.SerializeToElement(arguments));
    }

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
        if (PublishCommand.ExecutionTask is { } work) await work.ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Clear();
        PublishCommand.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await DrainAsync().ConfigureAwait(false);
    }
}
