using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.App.Resources;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class PullRequestInboxViewModel : ObservableObject, IDisposable, IAsyncDisposable
{
    private readonly SourceInboxReader _reader;
    private readonly CancellationTokenSource _shutdown = new();
    private CancellationTokenSource? _operation;
    private SourceAccountItemViewModel? _account;
    private PullRequestInboxFilterItem _selectedFilter;
    private string _status = string.Empty;
    private bool _busy;
    private bool _disposed;
    private bool _loaded;
    private bool _next;
    private bool _incomplete;
    private int _page = 1;
    private int _generation;

    public PullRequestInboxViewModel(SourceInboxReader reader)
    {
        _reader = reader;
        Filters = Enum.GetValues<PullRequestInboxFilter>()
            .Select(filter => new PullRequestInboxFilterItem(filter, PullRequestText.Get("InboxFilter_" + filter))).ToArray();
        _selectedFilter = Filters[0];
        LoadCommand = new(token => LoadAsync(1, token), CanRead);
        PreviousCommand = new(token => LoadAsync(_page - 1, token), () => CanRead() && _page > 1);
        NextCommand = new(token => LoadAsync(_page + 1, token), () => CanRead() && _next);
        CancelCommand = new RelayCommand(() => _operation?.Cancel(), () => _busy);
    }

    public ObservableCollection<PullRequestInboxItem> Items { get; } = [];
    public IReadOnlyList<PullRequestInboxFilterItem> Filters { get; }
    public GuardedAsyncRelayCommand LoadCommand { get; }
    public GuardedAsyncRelayCommand PreviousCommand { get; }
    public GuardedAsyncRelayCommand NextCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public string Status { get => _status; private set { SetProperty(ref _status, value); OnPropertyChanged(nameof(HasStatus)); } }
    public bool HasStatus => Status.Length > 0;
    public string PageLabel => string.Format(CultureInfo.CurrentCulture, PullRequestText.Get("PageLabel"), _page, Items.Count);
    public int Page => _page;
    public bool IsBusy => _busy;
    public bool IsLoaded => _loaded;
    public bool IsIncomplete => _incomplete;
    public bool IsEmpty => _loaded && !_busy && Items.Count == 0;

    public PullRequestInboxFilterItem? SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (value is null || !Filters.Contains(value) || !SetProperty(ref _selectedFilter, value)) return;
            _page = 1; _ = LoadCommand.ExecuteAsync(null);
        }
    }

    public void SetAccount(SourceAccountItemViewModel? account)
    {
        _account = account;
        Clear();
    }

    public void Clear()
    {
        _generation++; _operation?.Cancel();
        Items.Clear(); _page = 1; _next = _loaded = _incomplete = false;
        Status = string.Empty; Notify();
    }

    private bool CanRead() => !_disposed && !_busy && _account is not null;

    private async Task LoadAsync(int page, CancellationToken caller)
    {
        if (!CanRead() || _account is not { } account) return;
        var generation = _generation;
        var filter = SelectedFilter?.Filter ?? PullRequestInboxFilter.Involves;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(caller, _shutdown.Token);
        operation.CancelAfter(TimeSpan.FromSeconds(40));
        _operation = operation; _busy = true; _loaded = false; _incomplete = false;
        Items.Clear(); Status = PullRequestText.Get("Loading"); Notify();
        try
        {
            var result = await _reader.ReadAsync(account.Id, account.Revision, filter, page, operation.Token).ConfigureAwait(false);
            await PublishAsync(() =>
            {
                if (operation.IsCancellationRequested || generation != _generation) return;
                foreach (var item in result.Items) Items.Add(item);
                _page = result.Page; _next = result.HasNextPage; _incomplete = result.IsIncomplete; _loaded = true;
                Status = PullRequestText.Get(result.IsIncomplete ? "InboxIncomplete" : "Loaded");
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { await ReportAsync(generation, PullRequestText.Get("Cancelled")).ConfigureAwait(false); }
        catch (SourceReviewException exception) { await ReportAsync(generation, PullRequestText.Failure(exception.ErrorCode)).ConfigureAwait(false); }
        catch (Exception) { await ReportAsync(generation, PullRequestText.Get("Failed")).ConfigureAwait(false); }
        finally
        {
            _operation = null;
            await PublishAsync(() => { _busy = false; Notify(); }).ConfigureAwait(false);
        }
    }

    private Task ReportAsync(int generation, string status) => PublishAsync(() =>
    { if (generation == _generation) Status = status; });

    private Task PublishAsync(Action action) => AvaloniaUiThread.RunAsync(() => { if (!_disposed) action(); }, CancellationToken.None);

    private void Notify()
    {
        LoadCommand.NotifyCanExecuteChanged(); PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(Page)); OnPropertyChanged(nameof(PageLabel));
        OnPropertyChanged(nameof(IsBusy)); OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsLoaded)); OnPropertyChanged(nameof(IsIncomplete));
    }

    public async Task DrainAsync()
    {
        foreach (var command in new[] { LoadCommand, PreviousCommand, NextCommand })
            if (command.ExecutionTask is { } work) await work.ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        Clear(); _disposed = true; _shutdown.Cancel();
        LoadCommand.Dispose(); PreviousCommand.Dispose(); NextCommand.Dispose();
    }

    public async ValueTask DisposeAsync() { Dispose(); await DrainAsync().ConfigureAwait(false); _shutdown.Dispose(); }
}
