using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceSidebarSearchViewModel : ObservableObject, IDisposable
{
    private readonly WorkspaceCatalogViewModel _catalog;
    private readonly HashSet<WorkspaceProjectNavigationViewModel> _groups = [];
    private bool _hasMatches;
    private bool _disposed;

    public WorkspaceSidebarSearchViewModel(WorkspaceCatalogViewModel catalog)
    {
        _catalog = catalog;
        ClearCommand = new RelayCommand(ClearQuery, CanClear);
        catalog.TaskSearch.ClearCommand.CanExecuteChanged += HandleClearAvailabilityChanged;
        catalog.TaskSearch.PropertyChanged += HandleSearchChanged;
        catalog.ProjectNavigation.CollectionChanged += HandleGroupsChanged;
        catalog.TaskManagement.PinnedTasks.PropertyChanged += HandleResultsChanged;
        catalog.RecentTasks.PropertyChanged += HandleResultsChanged;
        SynchronizeGroups();
    }

    public string Query
    {
        get => _catalog.TaskSearch.Query;
        set { if (!_disposed) _catalog.TaskSearch.Query = value ?? string.Empty; }
    }

    public IRelayCommand ClearCommand { get; }
    public bool HasMatches { get => _hasMatches; private set => SetProperty(ref _hasMatches, value); }

    private void HandleSearchChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(WorkspaceTaskSearchViewModel.Query)) return;
        OnPropertyChanged(nameof(Query));
        Refresh();
    }

    private void HandleGroupsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (_disposed || args.Action == NotifyCollectionChangedAction.Move) return;
        if (args.Action == NotifyCollectionChangedAction.Reset)
        {
            SynchronizeGroups();
            return;
        }
        if (args.OldItems is not null)
        {
            foreach (WorkspaceProjectNavigationViewModel group in args.OldItems)
            {
                if (!_catalog.ProjectNavigation.Contains(group) && _groups.Remove(group))
                    group.PropertyChanged -= HandleResultsChanged;
            }
        }
        if (args.NewItems is not null)
        {
            foreach (WorkspaceProjectNavigationViewModel group in args.NewItems)
            {
                if (_groups.Add(group)) group.PropertyChanged += HandleResultsChanged;
            }
        }
        Refresh();
    }

    private void SynchronizeGroups()
    {
        foreach (var group in _groups) group.PropertyChanged -= HandleResultsChanged;
        _groups.Clear();
        foreach (var group in _catalog.ProjectNavigation)
        {
            if (_groups.Add(group)) group.PropertyChanged += HandleResultsChanged;
        }
        Refresh();
    }

    private void HandleResultsChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(WorkspaceProjectNavigationViewModel.IsVisible)
            or nameof(WorkspacePinnedTasksViewModel.HasItems)) Refresh();
    }

    private void Refresh()
    {
        if (_disposed) return;
        HasMatches = _catalog.ProjectNavigation.Any(group => group.IsVisible)
            || _catalog.TaskManagement.PinnedTasks.HasItems || _catalog.RecentTasks.HasItems;
    }

    private void HandleClearAvailabilityChanged(object? sender, EventArgs args) => ClearCommand.NotifyCanExecuteChanged();

    private bool CanClear() => !_disposed && _catalog.TaskSearch.ClearCommand.CanExecute(null);
    private void ClearQuery()
    {
        if (CanClear()) _catalog.TaskSearch.ClearCommand.Execute(null);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _catalog.TaskSearch.ClearCommand.CanExecuteChanged -= HandleClearAvailabilityChanged;
        ClearCommand.NotifyCanExecuteChanged();
        _catalog.TaskSearch.PropertyChanged -= HandleSearchChanged;
        _catalog.ProjectNavigation.CollectionChanged -= HandleGroupsChanged;
        _catalog.TaskManagement.PinnedTasks.PropertyChanged -= HandleResultsChanged;
        _catalog.RecentTasks.PropertyChanged -= HandleResultsChanged;
        foreach (var group in _groups) group.PropertyChanged -= HandleResultsChanged;
        _groups.Clear();
    }
}
