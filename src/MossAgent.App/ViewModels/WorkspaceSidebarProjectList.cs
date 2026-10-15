using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using MossAgent.Application.Settings;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceSidebarProjectList : IDisposable
{
    private readonly ObservableCollection<WorkspaceProjectNavigationViewModel> _source;
    private readonly HashSet<WorkspaceProjectNavigationViewModel> _subscriptions = [];
    private SidebarPreference _preference = new(SidebarProjectSortOrder.UpdatedAt, []);
    private bool _disposed;

    public WorkspaceSidebarProjectList(ObservableCollection<WorkspaceProjectNavigationViewModel> source)
    {
        _source = source;
        _source.CollectionChanged += HandleCollectionChanged;
        RefreshSubscriptions();
        Refresh();
    }

    public ObservableCollection<WorkspaceProjectNavigationViewModel> Items { get; } = [];

    public void SetPreference(SidebarPreference preference)
    {
        if (_disposed) return;
        _preference = preference;
        Refresh();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _source.CollectionChanged -= HandleCollectionChanged;
        foreach (var item in _subscriptions) item.PropertyChanged -= HandleProjectChanged;
        _subscriptions.Clear();
    }

    private void HandleCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (_disposed) return;
        if (args.Action == NotifyCollectionChangedAction.Reset) RefreshSubscriptions();
        else if (args.Action != NotifyCollectionChangedAction.Move)
        {
            if (args.OldItems is not null)
            {
                foreach (WorkspaceProjectNavigationViewModel item in args.OldItems)
                {
                    if (!_source.Contains(item) && _subscriptions.Remove(item))
                        item.PropertyChanged -= HandleProjectChanged;
                }
            }
            if (args.NewItems is not null)
            {
                foreach (WorkspaceProjectNavigationViewModel item in args.NewItems)
                {
                    if (_subscriptions.Add(item)) item.PropertyChanged += HandleProjectChanged;
                }
            }
        }
        Refresh();
    }

    private void RefreshSubscriptions()
    {
        foreach (var item in _subscriptions) item.PropertyChanged -= HandleProjectChanged;
        _subscriptions.Clear();
        foreach (var item in _source)
        {
            if (_subscriptions.Add(item)) item.PropertyChanged += HandleProjectChanged;
        }
    }

    private void HandleProjectChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(WorkspaceProjectNavigationViewModel.LastActivityAt)
            or nameof(WorkspaceProjectNavigationViewModel.Project)) Refresh();
    }

    private void Refresh()
    {
        if (_disposed) return;
        var ranks = _preference.ProjectSortOrder == SidebarProjectSortOrder.Manual
            ? _preference.ManualProjectOrder.Select((id, index) => (id, index))
                .ToDictionary(entry => entry.id, entry => entry.index) : null;
        var ordered = ranks is not null
            ? _source.OrderBy(item => ranks.GetValueOrDefault(item.Project.Id, int.MaxValue))
                .ThenByDescending(item => item.Project.CreatedAt)
            : _source.OrderByDescending(item => item.LastActivityAt)
                .ThenByDescending(item => item.Project.CreatedAt);
        var desired = ordered.ThenBy(item => item.Project.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Project.Id).ToArray();
        Items.SynchronizeWith(desired);
    }
}
