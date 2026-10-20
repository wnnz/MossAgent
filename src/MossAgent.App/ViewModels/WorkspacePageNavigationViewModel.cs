using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspacePageNavigationViewModel : ObservableObject, IDisposable
{
    private readonly WorkspaceCatalogViewModel _catalog;
    private bool _isAutomationsOpen;
    private bool _isPullRequestsOpen;

    public WorkspacePageNavigationViewModel(WorkspaceCatalogViewModel catalog, AutomationViewModel? automations,
        PullRequestsViewModel? pullRequests = null, WorkspaceComposerDraftsViewModel? drafts = null)
    {
        _catalog = catalog;
        Automations = automations;
        PullRequests = pullRequests;
        NewConversationCommand = new RelayCommand(() =>
        { ShowConversation(); catalog.NewTaskCommand.Execute(null); }, () => catalog.NewTaskCommand.CanExecute(null));
        OpenAutomationsCommand = new RelayCommand(() =>
        { IsAutomationsOpen = true; _ = Automations!.ReloadAsync(); }, () => Automations is not null);
        OpenPullRequestsCommand = new RelayCommand(() =>
        { IsPullRequestsOpen = true; _ = PullRequests!.OpenAsync(); }, () => PullRequests is not null);
        catalog.PropertyChanged += HandleCatalogChanged;
        catalog.ConversationRequested += ShowConversation;
        if (pullRequests?.Chat is { } chat)
        {
            chat.ConversationRequested += OpenReviewConversation;
            if (drafts is not null) chat.PrepareDraft = (task, text, token) => drafts.TrySeedAsync(new(null, task.Id), text, token);
        }
        if (automations is not null) { automations.History.IsVisible = false; automations.TaskOpened += ShowConversation; }
    }

    public AutomationViewModel? Automations { get; }
    public PullRequestsViewModel? PullRequests { get; }
    public IRelayCommand OpenPullRequestsCommand { get; }
    public IRelayCommand? PullRequestsNavigationCommand => PullRequests is null ? null : OpenPullRequestsCommand;
    public bool IsConversationOpen => !IsAutomationsOpen && !IsPullRequestsOpen;
    public bool IsPullRequestsOpen
    {
        get => _isPullRequestsOpen;
        set
        {
            if (!SetProperty(ref _isPullRequestsOpen, value)) return;
            if (value) IsAutomationsOpen = false; else PullRequests?.Leave();
            OnPropertyChanged(nameof(IsConversationOpen));
        }
    }
    public IRelayCommand NewConversationCommand { get; }
    public IRelayCommand OpenAutomationsCommand { get; }
    public bool IsAutomationsOpen
    {
        get => _isAutomationsOpen;
        set
        {
            if (!SetProperty(ref _isAutomationsOpen, value)) return;
            if (value) IsPullRequestsOpen = false;
            if (Automations is not null) Automations.History.IsVisible = value;
            OnPropertyChanged(nameof(IsConversationOpen));
        }
    }
    private void ShowConversation() { IsAutomationsOpen = false; IsPullRequestsOpen = false; }
    private void OpenReviewConversation(AgentTask task)
    {
        if (_catalog.IsLocked) return;
        _catalog.UpsertTask(task, select: false);
        _catalog.SelectTaskCommand.Execute(task);
    }
    private void HandleCatalogChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(WorkspaceCatalogViewModel.IsLocked)) NewConversationCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        _catalog.PropertyChanged -= HandleCatalogChanged;
        _catalog.ConversationRequested -= ShowConversation;
        if (PullRequests?.Chat is { } chat) { chat.ConversationRequested -= OpenReviewConversation; chat.PrepareDraft = null; }
        PullRequests?.Leave();
        if (Automations is not null) { Automations.History.IsVisible = false; Automations.TaskOpened -= ShowConversation; }
    }
}
