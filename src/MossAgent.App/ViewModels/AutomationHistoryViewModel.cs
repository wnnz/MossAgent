using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Automations;
using MossAgent.Application.Persistence;
using MossAgent.App.Resources;

namespace MossAgent.App.ViewModels;

public sealed class AutomationHistoryViewModel : ObservableObject, IDisposable
{
    private readonly IAutomationRepository _repository;
    private readonly IWorkspaceRepository _projects;
    private readonly WorkspaceCatalogViewModel _catalog;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _loadCancellation;
    private CancellationTokenSource? _openCancellation;
    private AutomationItemViewModel? _selection;
    private string? _loadError;
    private string? _openError;
    private bool _loading;
    private bool _opening;
    private bool _blocked;
    private bool _visible = true;
    private bool _disposed;
    private int _version;
    private int _navigationVersion;

    public AutomationHistoryViewModel(IAutomationRepository repository, IWorkspaceRepository projects,
        WorkspaceCatalogViewModel catalog)
    {
        _repository = repository;
        _projects = projects;
        _catalog = catalog;
        RefreshCommand = new AsyncRelayCommand(() => SelectAsync(_selection),
            () => !_disposed && IsVisible && !IsBlocked && !IsLoading && !IsOpening && HasSelection);
        OpenCommand = new AsyncRelayCommand<AutomationRunViewModel>(OpenAsync, CanOpen);
        _catalog.PropertyChanged += HandleCatalogChanged;
    }

    public event Action? TaskOpened;
    public ObservableCollection<AutomationRunViewModel> Runs { get; } = [];
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand<AutomationRunViewModel> OpenCommand { get; }
    public string SelectionName => _selection?.Name ?? AutomationText.SelectSchedule;
    public bool HasSelection => _selection is not null;
    public bool HasRuns => !IsLoading && !HasLoadError && Runs.Count > 0;
    public bool IsEmpty => HasSelection && !IsLoading && !HasLoadError && Runs.Count == 0;
    public bool IsLoading { get => _loading; private set { if (SetProperty(ref _loading, value)) NotifyState(); } }
    public bool IsOpening { get => _opening; private set { if (SetProperty(ref _opening, value)) NotifyCommands(); } }
    public bool IsBlocked
    {
        get => _blocked;
        set { if (SetProperty(ref _blocked, value)) { if (value) InvalidateNavigation(); NotifyCommands(); } }
    }
    public bool IsVisible
    {
        get => _visible;
        set { if (SetProperty(ref _visible, value)) { if (!value) InvalidateNavigation(); NotifyCommands(); } }
    }
    public string? LoadError { get => _loadError; private set { if (SetProperty(ref _loadError, value)) NotifyState(); } }
    public string? OpenError { get => _openError; private set { if (SetProperty(ref _openError, value)) OnPropertyChanged(nameof(HasOpenError)); } }
    public bool HasLoadError => LoadError is not null;
    public bool HasOpenError => OpenError is not null;

    public async Task SelectAsync(AutomationItemViewModel? item)
    {
        if (_disposed) return;
        var version = ++_version;
        InvalidateNavigation();
        _loadCancellation?.Cancel();
        _selection = item;
        Runs.Clear();
        LoadError = null;
        OpenError = null;
        IsLoading = item is not null;
        NotifyState();
        if (item is null) return;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        cancellation.CancelAfter(TimeSpan.FromSeconds(30));
        _loadCancellation = cancellation;
        try
        {
            var runs = await _repository.GetRunsAsync(item.Schedule.Id, 50, cancellation.Token);
            if (!IsCurrent(version)) return;
            cancellation.Token.ThrowIfCancellationRequested();
            var published = 0;
            foreach (var run in runs)
            {
                if (run.ScheduleId != item.Schedule.Id) continue;
                Runs.Add(new AutomationRunViewModel(run));
                if (++published == 50) break;
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested || !IsCurrent(version)) { }
        catch (Exception) { if (IsCurrent(version)) LoadError = AutomationText.Get("HistoryLoadError"); }
        finally
        {
            if (ReferenceEquals(_loadCancellation, cancellation)) _loadCancellation = null;
            if (IsCurrent(version)) { IsLoading = false; NotifyState(); }
        }
    }

    private bool CanOpen(AutomationRunViewModel? item) => !_disposed && IsVisible && !IsBlocked && !IsLoading && !IsOpening
        && !_catalog.IsLocked && item is not null && Runs.Contains(item) && item.Run.ScheduleId == _selection?.Schedule.Id;

    private async Task OpenAsync(AutomationRunViewModel? item)
    {
        if (!CanOpen(item) || _selection is not { } selected) return;
        var version = _version;
        var navigationVersion = _navigationVersion;
        IsOpening = true;
        OpenError = null;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        cancellation.CancelAfter(TimeSpan.FromSeconds(30));
        _openCancellation = cancellation;
        try
        {
            var tasks = await _projects.GetTasksAsync(selected.Schedule.ProjectId, cancellation.Token);
            if (!IsCurrent(version) || navigationVersion != _navigationVersion) return;
            cancellation.Token.ThrowIfCancellationRequested();
            var task = tasks.FirstOrDefault(task => task.Id == item!.Run.TaskId && task.ProjectId == selected.Schedule.ProjectId);
            if (task is null || IsBlocked || _catalog.IsLocked || !_catalog.Projects.Any(project => project.Id == task.ProjectId))
                OpenError = AutomationText.Get("TaskUnavailable");
            else { _catalog.SelectTaskCommand.Execute(task); TaskOpened?.Invoke(); }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested || !IsCurrent(version) || navigationVersion != _navigationVersion) { }
        catch (Exception) { if (IsCurrent(version) && navigationVersion == _navigationVersion) OpenError = AutomationText.Get("HistoryOpenError"); }
        finally { _openCancellation = null; IsOpening = false; }
    }

    private bool IsCurrent(int version) => !_disposed && version == _version;
    private void NotifyState()
    {
        OnPropertyChanged(nameof(SelectionName)); OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasRuns)); OnPropertyChanged(nameof(IsEmpty)); OnPropertyChanged(nameof(HasLoadError));
        NotifyCommands();
    }
    private void NotifyCommands() { RefreshCommand.NotifyCanExecuteChanged(); OpenCommand.NotifyCanExecuteChanged(); }
    private void HandleCatalogChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(WorkspaceCatalogViewModel.IsLocked)) return;
        if (_catalog.IsLocked) InvalidateNavigation();
        NotifyCommands();
    }
    private void InvalidateNavigation() { _navigationVersion++; _openCancellation?.Cancel(); }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _version++;
        _catalog.PropertyChanged -= HandleCatalogChanged;
        _lifetime.Cancel();
        _selection = null;
        Runs.Clear();
        LoadError = null;
        OpenError = null;
        IsLoading = false;
        NotifyState();
    }
}
