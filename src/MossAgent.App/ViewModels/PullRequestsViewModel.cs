using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.App.Resources;
using MossAgent.Application.Persistence;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class PullRequestsViewModel : ObservableObject, IDisposable, IAsyncDisposable
{
    private readonly ISourceAccountRepository _accounts;
    private readonly ISourceRepositoryGrants _grants;
    private readonly ISourceReviewSessions _sessions;
    private readonly IToolExecutor _tools;
    private readonly IPullRequestMarks _marks;
    private readonly CancellationTokenSource _shutdown = new();
    private CancellationTokenSource? _operation;
    private SourceRepositoryItemViewModel? _selectedRepository;
    private SourceAccountItemViewModel? _selectedAccount;
    private PullRequestItemViewModel? _selectedItem;
    private PullRequestSummary? _detail;
    private string _owner = string.Empty;
    private string _repository = string.Empty;
    private string _status = string.Empty;
    private bool _busy;
    private bool _disposed;
    private bool _isEditorOpen;
    private bool _editorCanWrite;
    private bool _hasNextPage;
    private bool _updating;
    private int _page = 1;
    private int _generation;

    public PullRequestsViewModel(ISourceAccountRepository accounts, ISourceRepositoryGrants grants,
        ISourceReviewSessions sessions, IToolExecutor tools, SourceInboxReader inbox,
        IPullRequestMarks marks, SourceReviewChatCreator? reviewChats = null)
    {
        _accounts = accounts; _grants = grants; _sessions = sessions; _tools = tools; _marks = marks;
        Inspection = new(tools);
        Inbox = new(inbox);
        Link = new(new SourcePullRequestLinkResolver(accounts, grants), Accounts, LoadLinkedAsync,
            () => !_disposed && !_busy && !IsEditorOpen);
        Link.PropertyChanged += LinkChanged;
        if (reviewChats is not null)
        {
            Chat = new(reviewChats, CaptureReview, () => Inspection.Runs.Items.Concat(Inspection.Statuses.Items).ToArray(),
                () => CanRead() && Detail is not null && Inspection.IsActive);
            Inspection.PropertyChanged += ReviewChanged;
            Inspection.Runs.Items.CollectionChanged += ReviewItemsChanged; Inspection.Statuses.Items.CollectionChanged += ReviewItemsChanged;
        }
        RefreshCommand = new(token => RunAsync(ReloadAsync, token), () => !IsBusy && !_disposed && !Link.IsOpen);
        LoadCommand = new(token => RunAsync(cancel => LoadPageAsync(1, cancel), token), CanRead);
        PreviousCommand = new(token => RunAsync(cancel => LoadPageAsync(_page - 1, cancel), token), () => CanRead() && _page > 1);
        NextCommand = new(token => RunAsync(cancel => LoadPageAsync(_page + 1, cancel), token), () => CanRead() && _hasNextPage);
        DetailCommand = new(token => RunAsync(LoadDetailAsync, token), () => CanRead() && SelectedItem is not null);
        SaveGrantCommand = new(token => RunAsync(SaveGrantAsync, token), () => !IsBusy && !_disposed && SelectedAccount is not null && IsEditorOpen);
        RevokeCommand = new(token => RunAsync(RevokeAsync, token), CanRead);
        CreateCommand = new RelayCommand(() => IsEditorOpen = true, () => !IsBusy && !_disposed && !Link.IsOpen);
        CloseEditorCommand = new RelayCommand(() => { IsEditorOpen = false; Owner = Repository = string.Empty; EditorCanWrite = false; }, () => !_busy);
        CancelCommand = new RelayCommand(() => { _operation?.Cancel(); Link.Cancel(); }, () => IsBusy);
        BackCommand = new RelayCommand(() => { Invalidate(); Detail = null; }, () => !_disposed && !IsBusy && !Link.IsOpen && HasDetail);
    }

    public ObservableCollection<SourceAccountItemViewModel> Accounts { get; } = [];
    public ObservableCollection<SourceRepositoryItemViewModel> Repositories { get; } = [];
    public ObservableCollection<PullRequestItemViewModel> Items { get; } = [];
    public PullRequestInspectionViewModel Inspection { get; }
    public PullRequestInboxViewModel Inbox { get; }
    public PullRequestLinkViewModel Link { get; }
    public PullRequestChatLaunchViewModel? Chat { get; }
    public bool HasChat => Chat is not null;
    public GuardedAsyncRelayCommand RefreshCommand { get; }
    public GuardedAsyncRelayCommand LoadCommand { get; }
    public GuardedAsyncRelayCommand PreviousCommand { get; }
    public GuardedAsyncRelayCommand NextCommand { get; }
    public GuardedAsyncRelayCommand DetailCommand { get; }
    public GuardedAsyncRelayCommand SaveGrantCommand { get; }
    public GuardedAsyncRelayCommand RevokeCommand { get; }
    public IRelayCommand CreateCommand { get; }
    public IRelayCommand CloseEditorCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand BackCommand { get; }
    public bool IsBusy => _busy || Link.IsBusy;
    public bool IsEmpty => !IsBusy && Items.Count == 0 && !HasDetail;
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public int Page => _page;
    public string Owner { get => _owner; set => SetProperty(ref _owner, value); }
    public string Repository { get => _repository; set => SetProperty(ref _repository, value); }
    public bool IsEditorOpen { get => _isEditorOpen; set { SetProperty(ref _isEditorOpen, value); NotifyCommands(); } }
    public bool EditorCanWrite { get => _editorCanWrite; set => SetProperty(ref _editorCanWrite, value); }
    public SourceAccountItemViewModel? SelectedAccount
    {
        get => _selectedAccount;
        set { SetProperty(ref _selectedAccount, value); Inbox.SetAccount(value); NotifyCommands(); }
    }
    public PullRequestItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set { if (SetProperty(ref _selectedItem, value)) { Invalidate(); Detail = null; NotifyCommands(); } }
    }
    public PullRequestSummary? Detail
    {
        get => _detail;
        private set { SetProperty(ref _detail, value); OnPropertyChanged(nameof(HasDetail)); OnPropertyChanged(nameof(IsEmpty)); BackCommand.NotifyCanExecuteChanged(); }
    }
    public bool HasDetail => Detail is not null;
    public SourceRepositoryItemViewModel? SelectedRepository
    {
        get => _selectedRepository;
        set
        {
            if (!SetProperty(ref _selectedRepository, value)) return;
            Invalidate(); Items.Clear(); SelectedItem = null; Detail = null;
            _page = 1; _hasNextPage = false; Status = string.Empty;
            OnPropertyChanged(nameof(Page)); OnPropertyChanged(nameof(IsEmpty)); NotifyCommands();
        }
    }

    public Task OpenAsync() => RefreshCommand.ExecuteAsync(null);
    public void Leave() { Link.Close(); Invalidate(); Detail = null; Items.Clear(); SelectedItem = null; OnPropertyChanged(nameof(IsEmpty)); }
    private bool CanRead() => !_disposed && !IsBusy && !Link.IsOpen && SelectedRepository is not null;
    private void Invalidate() { if (_updating) return; _generation++; _operation?.Cancel(); Link.Cancel(); Chat?.Invalidate(); Inspection.Clear(); Inbox.Clear(); }
    private void LinkChanged(object? sender, PropertyChangedEventArgs args)
    { if (args.PropertyName is nameof(Link.IsBusy) or nameof(Link.IsOpen)) NotifyCommands(); }

    private SourceReviewChatBinding? CaptureReview() => Detail is { } detail && SelectedRepository is { } repository && Inspection.IsActive
        ? new(Guid.NewGuid(), repository.Scope, new(detail.Number, detail.BaseRevision, detail.HeadRevision),
            SourceReviewContextBuilder.Build(repository.Scope, detail, Inspection.Files.Items.ToArray(),
                Inspection.Conversation.Items.Concat(Inspection.Inline.Items).ToArray(), Inspection.Runs.Items.Concat(Inspection.Statuses.Items).ToArray())) : null;
    private void ReviewChanged(object? sender, PropertyChangedEventArgs args)
    { if (args.PropertyName == nameof(Inspection.IsActive)) { if (!Inspection.IsActive) Chat?.Invalidate(); Chat?.NotifyAvailability(); } }
    private void ReviewItemsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs args) => Chat?.NotifyAvailability();

    private async Task ReloadAsync(CancellationToken token)
    {
        var accounts = await Task.Run(() => _accounts.GetAllAsync(token), token).ConfigureAwait(false);
        var grants = await _grants.GetAllAsync(token).ConfigureAwait(false);
        await PublishAsync(() =>
        {
            if (token.IsCancellationRequested) return;
            _updating = true;
            try
            {
                var previous = SelectedRepository?.Scope.GrantId;
                Accounts.Clear(); Repositories.Clear(); SelectedRepository = null;
                foreach (var account in accounts.Where(account => account.IsEnabled)) Accounts.Add(new(account));
                foreach (var grant in grants)
                {
                    var account = accounts.FirstOrDefault(account => account.Id == grant.AccountId);
                    Repositories.Add(new(grant, account?.Name ?? PullRequestText.Get("MissingAccount")));
                }
                SelectedAccount = Accounts.FirstOrDefault();
                SelectedRepository = Repositories.FirstOrDefault(item => item.Scope.GrantId == previous) ?? Repositories.FirstOrDefault();
                Status = PullRequestText.Get("Loaded");
            }
            finally { _updating = false; }
        }).ConfigureAwait(false);
    }

    private async Task SaveGrantAsync(CancellationToken token)
    {
        var account = SelectedAccount!;
        var scope = new SourceRepositoryScope(Guid.NewGuid(), account.Id, account.Revision,
            SourceRepositoryScope.ValidateSegment(Owner.Trim(), true), SourceRepositoryScope.ValidateSegment(Repository.Trim()),
            EditorCanWrite);
        await Task.Run(() => _grants.AddAsync(scope, token), token).ConfigureAwait(false);
        await ReloadAsync(token).ConfigureAwait(false);
        await PublishAsync(() =>
        {
            if (token.IsCancellationRequested) return;
            _updating = true;
            SelectedRepository = Repositories.FirstOrDefault(item => item.Scope.GrantId == scope.GrantId);
            _updating = false;
            IsEditorOpen = false; Owner = Repository = string.Empty; EditorCanWrite = false;
            Status = PullRequestText.Get("Granted");
        }).ConfigureAwait(false);
    }

    private async Task RevokeAsync(CancellationToken token)
    {
        var scope = SelectedRepository!.Scope;
        await Task.Run(() => _grants.RevokeAsync(scope.GrantId, token), token).ConfigureAwait(false);
        await ReloadAsync(token).ConfigureAwait(false);
        await PublishAsync(() => Status = PullRequestText.Get("Revoked")).ConfigureAwait(false);
    }

    private async Task LoadPageAsync(int page, CancellationToken token)
    {
        var scope = SelectedRepository!.Scope;
        var generation = _generation;
        var context = await ContextAsync(scope, token).ConfigureAwait(false);
        var result = await _tools.ExecuteAsync(new(Guid.NewGuid().ToString("N"), "source.pr_list",
            JsonSerializer.SerializeToElement(new { page })), context, token).ConfigureAwait(false);
        if (!result.IsSuccess) throw new SourceReviewException(result.ErrorCode ?? "source_unavailable");
        var payload = JsonSerializer.Deserialize<PullRequestPage>(result.Content!) ?? throw new SourceReviewException("source_invalid_response");
        var marks = await _marks.GetAsync(scope.AccountId, scope.Owner, scope.Repository,
            payload.Items.Select(item => item.Number).ToArray(), token).ConfigureAwait(false);
        var heads = marks.ToDictionary(mark => mark.Number, mark => mark.HeadRevision);
        await PublishAsync(() =>
        {
            if (token.IsCancellationRequested || generation != _generation) return;
            _updating = true;
            Items.Clear(); SelectedItem = null; Detail = null;
            _updating = false;
            foreach (var item in payload.Items)
                Items.Add(new(item, heads.TryGetValue(item.Number, out var head) ? head : null));
            _page = payload.Page; _hasNextPage = payload.HasNextPage;
            Status = PullRequestText.Get("Loaded");
            OnPropertyChanged(nameof(Page)); OnPropertyChanged(nameof(IsEmpty));
        }).ConfigureAwait(false);
    }

    private async Task LoadDetailAsync(CancellationToken token)
    {
        var scope = SelectedRepository!.Scope;
        var number = SelectedItem!.Summary.Number;
        var generation = _generation;
        var context = await ContextAsync(scope, token).ConfigureAwait(false);
        var result = await _tools.ExecuteAsync(new(Guid.NewGuid().ToString("N"), "source.pr_get",
            JsonSerializer.SerializeToElement(new { number })), context, token).ConfigureAwait(false);
        if (!result.IsSuccess) throw new SourceReviewException(result.ErrorCode ?? "source_unavailable");
        var detail = JsonSerializer.Deserialize<PullRequestSummary>(result.Content!) ?? throw new SourceReviewException("source_invalid_response");
        var marked = await TryMarkViewedAsync(scope, detail.Number, detail.HeadRevision, token).ConfigureAwait(false);
        await PublishAsync(() =>
        {
            if (!token.IsCancellationRequested && generation == _generation)
            {
                Detail = detail; Status = PullRequestText.Get("Loaded");
                if (marked) Items.FirstOrDefault(item => item.Summary.Number == detail.Number)?.ApplyMark(detail.HeadRevision);
                Inspection.Attach(context, new(detail.Number, detail.BaseRevision, detail.HeadRevision));
            }
        }).ConfigureAwait(false);
    }

    private async Task LoadLinkedAsync(SourcePullRequestTarget target, CancellationToken token)
    {
        var generation = _generation;
        await PublishAsync(() => { Chat?.Invalidate(); Inspection.Clear(); Detail = null; }).ConfigureAwait(false);
        await Inspection.DrainAsync().ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        var context = await ContextAsync(target.Scope, token).ConfigureAwait(false);
        var result = await _tools.ExecuteAsync(new(Guid.NewGuid().ToString("N"), "source.pr_get",
            JsonSerializer.SerializeToElement(new { number = target.Number })), context, token).ConfigureAwait(false);
        if (!result.IsSuccess) throw new SourceReviewException(result.ErrorCode ?? "source_unavailable");
        var detail = JsonSerializer.Deserialize<PullRequestSummary>(result.Content!) ?? throw new SourceReviewException("source_invalid_response");
        var marks = await _marks.GetAsync(target.Scope.AccountId, target.Scope.Owner, target.Scope.Repository,
            [detail.Number], token).ConfigureAwait(false);
        var marked = await TryMarkViewedAsync(target.Scope, detail.Number, detail.HeadRevision, token).ConfigureAwait(false);
        var linked = new PullRequestItemViewModel(detail, marked ? detail.HeadRevision : marks.FirstOrDefault()?.HeadRevision);
        await PublishAsync(() =>
        {
            if (token.IsCancellationRequested || generation != _generation) return;
            _updating = true;
            try
            {
                var repository = Repositories.FirstOrDefault(item => item.Scope == target.Scope);
                if (repository is null)
                {
                    repository = new(target.Scope, Accounts.FirstOrDefault(account => account.Id == target.Scope.AccountId)?.Name ?? PullRequestText.Get("MissingAccount"));
                    Repositories.Add(repository);
                }
                SelectedRepository = repository;
                Items.Clear(); Items.Add(linked); SelectedItem = linked; Detail = detail;
                _page = 1; _hasNextPage = false; OnPropertyChanged(nameof(Page));
                Status = PullRequestText.Get("LinkLoaded");
                Inspection.Attach(context, new(detail.Number, detail.BaseRevision, detail.HeadRevision));
            }
            finally { _updating = false; }
        }).ConfigureAwait(false);
    }

    // Bookkeeping is best effort: persistence failures must not fail detail loading, but
    // cancellation still propagates so the operation reports "Cancelled" honestly.
    private async Task<bool> TryMarkViewedAsync(SourceRepositoryScope scope, int number, string headRevision,
        CancellationToken token)
    {
        try
        {
            await _marks.MarkViewedAsync(scope.AccountId, scope.Owner, scope.Repository, number,
                headRevision, token).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException) { return false; }
    }

    private async Task<ToolExecutionContext> ContextAsync(SourceRepositoryScope scope, CancellationToken token) =>
        new(await Task.Run(() => _sessions.GetOrCreateAsync(scope, token), token).ConfigureAwait(false), string.Empty, [], ApprovalPolicy.ReadOnly,
            MaximumOutputCharacters: 1024 * 1024, Timeout: TimeSpan.FromSeconds(30))
        { SourceScope = scope, AllowedToolNames = new HashSet<string> { "source.pr_list", "source.pr_get", "source.pr_files",
            "source.pr_comments", "source.pr_checks", "source.pr_reviews", "source.pr_activity", "source.pr_comment_create" } };

    private async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken caller)
    {
        if (_disposed || IsBusy) return;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(caller, _shutdown.Token);
        operation.CancelAfter(TimeSpan.FromSeconds(40));
        _operation = operation; _busy = true;
        Inspection.Clear();
        Status = PullRequestText.Get("Loading"); NotifyCommands();
        try
        {
            await Inspection.DrainAsync().ConfigureAwait(false);
            operation.Token.ThrowIfCancellationRequested();
            await action(operation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { await PublishAsync(() => Status = PullRequestText.Get("Cancelled")).ConfigureAwait(false); }
        catch (SourceReviewException exception)
        { await PublishAsync(() => Status = PullRequestText.Failure(exception.ErrorCode)).ConfigureAwait(false); }
        catch (ArgumentException) { await PublishAsync(() => Status = PullRequestText.Get("InvalidRepository")).ConfigureAwait(false); }
        catch (Exception) { await PublishAsync(() => Status = PullRequestText.Get("Failed")).ConfigureAwait(false); }
        finally
        {
            _operation = null;
            await PublishAsync(() => { _busy = false; NotifyCommands(); OnPropertyChanged(nameof(IsEmpty)); }).ConfigureAwait(false);
        }
    }

    private Task PublishAsync(Action action) => AvaloniaUiThread.RunAsync(() => { if (!_disposed) action(); }, CancellationToken.None);

    private void NotifyCommands()
    {
        foreach (var command in Commands()) command.NotifyCanExecuteChanged();
        CreateCommand?.NotifyCanExecuteChanged(); CloseEditorCommand?.NotifyCanExecuteChanged(); CancelCommand?.NotifyCanExecuteChanged();
        BackCommand?.NotifyCanExecuteChanged();
        Chat?.NotifyAvailability();
        Link?.NotifyAvailability();
        OnPropertyChanged(nameof(IsBusy)); OnPropertyChanged(nameof(IsEmpty));
    }

    private GuardedAsyncRelayCommand[] Commands() =>
        [RefreshCommand, LoadCommand, PreviousCommand, NextCommand, DetailCommand, SaveGrantCommand, RevokeCommand];

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _shutdown.Cancel(); Invalidate();
        Link.PropertyChanged -= LinkChanged; Link.Dispose();
        Inspection.PropertyChanged -= ReviewChanged;
        Inspection.Runs.Items.CollectionChanged -= ReviewItemsChanged; Inspection.Statuses.Items.CollectionChanged -= ReviewItemsChanged;
        foreach (var command in Commands()) command.Dispose();
        Inspection.Dispose();
        Inbox.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        foreach (var command in Commands()) if (command.ExecutionTask is { } work) await work.ConfigureAwait(false);
        await Inspection.DisposeAsync().ConfigureAwait(false);
        await Inbox.DisposeAsync().ConfigureAwait(false);
        await Link.DisposeAsync().ConfigureAwait(false);
        if (Chat is not null) await Chat.DisposeAsync().ConfigureAwait(false);
        _shutdown.Dispose();
    }
}
