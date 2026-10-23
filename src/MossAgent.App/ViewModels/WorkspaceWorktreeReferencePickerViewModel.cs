using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Git;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceWorktreeReferencePickerViewModel : ObservableObject
{
    internal const int PageSize = 20;
    private readonly IGitWorktreeReferenceDiscovery? _discovery;
    private readonly Func<ProjectProfile?> _project;
    private readonly Func<bool> _canChoose;
    private readonly Action<GitWorktreeReference> _select;
    private int _generation;
    private int _skip;
    private string _searchText = string.Empty;
    private bool _isBusy;
    private bool _hasMore;
    private bool _truncated;
    private string? _error;
    private WorkspaceWorktreeReferenceItemViewModel? _selectedItem;
    private CancellationTokenSource? _active;

    public WorkspaceWorktreeReferencePickerViewModel(IGitWorktreeReferenceDiscovery? discovery,
        Func<ProjectProfile?> project, Func<bool> canChoose, Action<GitWorktreeReference> select)
    {
        _discovery = discovery;
        _project = project;
        _canChoose = canChoose;
        _select = select;
        NextPageCommand = new AsyncRelayCommand(() => MoveAsync(PageSize), () => HasMore && !IsBusy);
        PreviousPageCommand = new AsyncRelayCommand(() => MoveAsync(-PageSize), () => _skip > 0 && !IsBusy);
    }

    internal TimeSpan SearchDelay { get; set; } = TimeSpan.FromMilliseconds(180);

    public ObservableCollection<WorkspaceWorktreeReferenceItemViewModel> Items { get; } = [];

    public bool IsAvailable => _discovery is not null;
    public string Label => WorkspaceText.Get("TaskEnvironmentReferenceLabel");
    public string SearchPlaceholder => WorkspaceText.Get("TaskEnvironmentReferenceSearchPlaceholder");
    public string EmptyText => WorkspaceText.Get("TaskEnvironmentReferenceEmpty");
    public string LoadingText => WorkspaceText.Get("TaskEnvironmentReferenceLoading");
    public string TruncatedText => WorkspaceText.Get("TaskEnvironmentReferenceTruncated");
    public string PreviousLabel => WorkspaceText.Get("TaskEnvironmentReferencePrevious");
    public string NextLabel => WorkspaceText.Get("TaskEnvironmentReferenceNext");

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value ?? string.Empty)) return;
            _skip = 0;
            ScheduleRefresh();
        }
    }

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public bool HasMore { get => _hasMore; private set => SetProperty(ref _hasMore, value); }
    public bool CanPrevious => _skip > 0;
    public string? StatusText => IsBusy ? LoadingText : _error ?? (Items.Count == 0 ? EmptyText : _truncated ? TruncatedText : null);
    public bool HasStatus => StatusText is not null || HasMore || CanPrevious;

    public WorkspaceWorktreeReferenceItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (!SetProperty(ref _selectedItem, value) || value is null) return;
            _select(value.Reference);
        }
    }

    public IAsyncRelayCommand NextPageCommand { get; }
    public IAsyncRelayCommand PreviousPageCommand { get; }

    internal void ResetContext()
    {
        Interlocked.Increment(ref _generation);
        CancelActive();
        _skip = 0;
        _selectedItem = null;
        _searchText = string.Empty;
        _error = null;
        Items.Clear();
        IsBusy = false;
        HasMore = false;
        _truncated = false;
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(SelectedItem));
        NotifyStatus();
    }

    internal void NotifyOpened()
    {
        if (_project() is null || !_canChoose()) return;
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        var generation = Interlocked.Increment(ref _generation);
        var project = _project();
        if (_discovery is null || project is null || !_canChoose()) return;
        IsBusy = true;
        var active = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _active, active);
        if (previous is not null) { try { previous.Cancel(); } catch (ObjectDisposedException) { } previous.Dispose(); }
        try
        {
            var page = await _discovery.SearchAsync(project, _searchText.Trim(), _skip, PageSize, active.Token).ConfigureAwait(true);
            if (generation != _generation) return;
            Items.Clear();
            if (_selectedItem is not null) { _selectedItem = null; OnPropertyChanged(nameof(SelectedItem)); }
            foreach (var reference in page.Items) Items.Add(new WorkspaceWorktreeReferenceItemViewModel(reference));
            HasMore = page.HasMore;
            _truncated = page.Truncated;
            _error = null;
        }
        catch (Exception failure)
        {
            if (generation != _generation || failure is OperationCanceledException) return;
            Items.Clear();
            _error = failure is UnauthorizedAccessException
                ? WorkspaceText.Get("TaskEnvironmentReferenceAuthorizationFailure")
                : WorkspaceText.Get("TaskEnvironmentReferenceFailure");
            HasMore = false;
            _truncated = false;
        }
        finally
        {
            if (ReferenceEquals(_active, active)) _active = null;
            active.Dispose();
            if (generation == _generation) IsBusy = false;
            NotifyStatus();
        }
    }

    private async Task MoveAsync(int delta)
    {
        var skip = Math.Max(0, _skip + delta);
        if (skip == _skip) return;
        _skip = skip;
        await RefreshAsync();
    }

    private async void ScheduleRefresh()
    {
        var generation = Interlocked.Increment(ref _generation);
        await Task.Delay(SearchDelay).ConfigureAwait(true);
        if (generation == _generation) _ = RefreshAsync();
    }

    private void CancelActive()
    {
        var active = Interlocked.Exchange(ref _active, null);
        if (active is null) return;
        try { active.Cancel(); } catch (ObjectDisposedException) { }
        active.Dispose();
    }

    private void NotifyStatus()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(HasStatus));
        OnPropertyChanged(nameof(CanPrevious));
        NextPageCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
    }
}
