using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.App.Resources;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class PullRequestCommentComposerViewModel : ObservableObject, IAsyncDisposable
{
    private const int MaximumBodyLength = 4096;
    private readonly IToolExecutor _tools;
    private readonly Func<Task> _reload;
    private CancellationTokenSource? _operation;
    private ToolExecutionContext? _context;
    private PullRequestRevision? _revision;
    private string _text = string.Empty;
    private string _status = string.Empty;
    private bool _busy;
    private bool _confirming;
    private bool _disposed;
    private int _generation;

    public PullRequestCommentComposerViewModel(IToolExecutor tools, Func<Task> reload)
    {
        _tools = tools;
        _reload = reload;
        PublishCommand = new(token => PublishAsync(token), CanPublish);
        CancelCommand = new RelayCommand(() => _operation?.Cancel(), () => _busy);
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
    public string PublishLabel => _confirming ? PullRequestText.CommentConfirm : PullRequestText.CommentPublish;
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
        _text = string.Empty;
        OnPropertyChanged(nameof(Text));
        Status = string.Empty;
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(CanWrite));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(PublishLabel));
        PublishCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    private bool CanPublish() => !_disposed && !_busy && CanWrite
        && Text.Trim() is { Length: > 0 and <= MaximumBodyLength };

    private async Task PublishAsync(CancellationToken caller)
    {
        if (!CanPublish()) return;
        if (!_confirming)
        {
            _confirming = true;
            Status = PullRequestText.CommentConfirmHint;
            OnPropertyChanged(nameof(PublishLabel));
            PublishCommand.NotifyCanExecuteChanged();
            return;
        }
        var generation = _generation;
        var context = _context!;
        var revision = _revision!;
        var body = Text.Trim();
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(caller);
        operation.CancelAfter(TimeSpan.FromSeconds(90));
        _operation = operation;
        _busy = true;
        _confirming = false;
        Status = PullRequestText.CommentPublishing;
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(PublishLabel));
        PublishCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        try
        {
            var result = await Task.Run(() => _tools.ExecuteAsync(Request(revision, body), WriteContext(context),
                operation.Token), operation.Token).ConfigureAwait(false);
            var reconciled = Reconciled(result);
            await PublishAsync(() =>
            {
                if (generation != _generation) return;
                if (operation.IsCancellationRequested)
                {
                    Status = PullRequestText.CommentCancelled;
                    return;
                }
                if (!result.IsSuccess)
                {
                    Status = PullRequestText.Failure(result.ErrorCode ?? "source_unavailable");
                    return;
                }
                Text = string.Empty;
                Status = PullRequestText.Get(reconciled ? "CommentLanded" : "CommentPublished");
            }).ConfigureAwait(false);
            if (result.IsSuccess && generation == _generation) _ = _reload();
        }
        catch (OperationCanceledException)
        { await PublishAsync(() => { if (generation == _generation) Status = PullRequestText.CommentCancelled; }).ConfigureAwait(false); }
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

    private static ToolRequest Request(PullRequestRevision revision, string body) =>
        new(Guid.NewGuid().ToString("N"), "source.pr_comment_create", JsonSerializer.SerializeToElement(
            new Dictionary<string, object>
            {
                ["number"] = revision.Number,
                ["base_revision"] = revision.BaseRevision,
                ["head_revision"] = revision.HeadRevision,
                ["body"] = body
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
