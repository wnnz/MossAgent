using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceConversationTabsViewModel : ObservableObject, IDisposable
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.App.Resources.WorkspaceStrings", typeof(WorkspaceConversationTabsViewModel).Assembly);
    private readonly WorkspaceCatalogViewModel _catalog;
    private Guid? _closingSelectedTaskId;
    private ConversationTabViewModel? _selectedTab;
    private bool _disposed;

    public WorkspaceConversationTabsViewModel(WorkspaceCatalogViewModel catalog)
    {
        _catalog = catalog;
        SelectTabCommand = new RelayCommand<ConversationTabViewModel>(SelectTab, CanNavigate);
        CloseTabCommand = new RelayCommand<ConversationTabViewModel>(CloseTab, CanNavigate);
        _catalog.SelectionChanged += Synchronize;
        _catalog.PropertyChanged += HandleCatalogPropertyChanged;
        Synchronize();
    }

    public ObservableCollection<ConversationTabViewModel> Items { get; } = [];
    public bool IsSingleTab => Items.Count == 1 && SelectedTab is not null;
    public bool ShowTabStrip => SelectedTab is not null && !IsSingleTab;
    public ConversationTabViewModel? SelectedTab
    {
        get => _selectedTab;
        private set => SetProperty(ref _selectedTab, value);
    }
    public IRelayCommand<ConversationTabViewModel> SelectTabCommand { get; }
    public IRelayCommand<ConversationTabViewModel> CloseTabCommand { get; }
    public string CloseTabLabel => Labels.GetString(nameof(CloseTabLabel))
        ?? throw new InvalidOperationException("Missing workspace resource: CloseTabLabel");
    public string TabOptionsLabel => Labels.GetString(nameof(TabOptionsLabel))
        ?? throw new InvalidOperationException("Missing workspace resource: TabOptionsLabel");

    private bool CanNavigate(ConversationTabViewModel? tab) =>
        !_disposed && tab is not null && Items.Contains(tab) && !_catalog.IsLocked;

    private void SelectTab(ConversationTabViewModel? tab)
    {
        if (!CanNavigate(tab)) return;
        _catalog.SelectTaskCommand.Execute(tab!.Task);
    }

    private void CloseTab(ConversationTabViewModel? tab)
    {
        if (!CanNavigate(tab)) return;
        var index = Items.IndexOf(tab!);
        var wasSelected = _catalog.SelectedTask?.Id == tab!.Task.Id;
        if (wasSelected) _closingSelectedTaskId = tab.Task.Id;
        Items.RemoveAt(index);
        NotifyCommands();
        if (!wasSelected) return;
        if (Items.Count == 0) _catalog.StartNewTask();
        else SelectTab(Items[Math.Min(index, Items.Count - 1)]);
    }

    private void Synchronize()
    {
        if (_disposed) return;
        var task = _catalog.SelectedTask;
        ConversationTabViewModel? selected = null;
        for (var index = Items.Count - 1; index >= 0; index--)
        {
            var tab = Items[index];
            var current = _catalog.FindTask(tab.Task.ProjectId, tab.Task.Id);
            if (current is null)
            {
                Items.RemoveAt(index);
                continue;
            }
            tab.Task = current;
            if (current.Id == task?.Id) selected = tab;
        }

        if (task?.Id != _closingSelectedTaskId)
            _closingSelectedTaskId = null;
        if (task is not null && task.Id != _closingSelectedTaskId)
        {
            if (selected is null) Items.Add(selected = new ConversationTabViewModel(task));
            else selected.Task = task;
        }
        for (var index = 0; index < Items.Count; index++)
            Items[index].IsSelected = Items[index].Task.Id == task?.Id;
        SelectedTab = selected;
        NotifyCommands();
    }

    private void HandleCatalogPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(WorkspaceCatalogViewModel.IsLocked)) NotifyCommands();
    }

    private void NotifyCommands()
    {
        OnPropertyChanged(nameof(IsSingleTab));
        OnPropertyChanged(nameof(ShowTabStrip));
        SelectTabCommand.NotifyCanExecuteChanged();
        CloseTabCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _catalog.SelectionChanged -= Synchronize;
        _catalog.PropertyChanged -= HandleCatalogPropertyChanged;
        Items.Clear();
        SelectedTab = null;
        NotifyCommands();
    }
}
