using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MossAgent.App.ViewModels;

public sealed class WorkspacePageNavigationViewModel : ObservableObject, IDisposable
{
    private readonly WorkspaceCatalogViewModel _catalog;
    private bool _isAutomationsOpen;

    public WorkspacePageNavigationViewModel(WorkspaceCatalogViewModel catalog, AutomationViewModel? automations)
    {
        _catalog = catalog;
        Automations = automations;
        NewConversationCommand = new RelayCommand(() =>
        { IsAutomationsOpen = false; catalog.NewTaskCommand.Execute(null); }, () => catalog.NewTaskCommand.CanExecute(null));
        OpenAutomationsCommand = new RelayCommand(() =>
        { IsAutomationsOpen = true; _ = Automations!.ReloadAsync(); }, () => Automations is not null);
        catalog.PropertyChanged += HandleCatalogChanged;
        catalog.ConversationRequested += ShowConversation;
        if (automations is not null) { automations.History.IsVisible = false; automations.TaskOpened += ShowConversation; }
    }

    public AutomationViewModel? Automations { get; }
    public IRelayCommand NewConversationCommand { get; }
    public IRelayCommand OpenAutomationsCommand { get; }
    public bool IsAutomationsOpen
    {
        get => _isAutomationsOpen;
        set { if (SetProperty(ref _isAutomationsOpen, value) && Automations is not null) Automations.History.IsVisible = value; }
    }
    private void ShowConversation() => IsAutomationsOpen = false;
    private void HandleCatalogChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(WorkspaceCatalogViewModel.IsLocked)) NewConversationCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        _catalog.PropertyChanged -= HandleCatalogChanged;
        _catalog.ConversationRequested -= ShowConversation;
        if (Automations is not null) { Automations.History.IsVisible = false; Automations.TaskOpened -= ShowConversation; }
    }
}
