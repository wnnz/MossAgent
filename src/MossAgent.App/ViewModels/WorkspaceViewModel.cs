using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Agent;
using MossAgent.Application.Git;
using MossAgent.Application.Persistence;
using MossAgent.Application.Settings;
using MossAgent.Domain;
using MossAgent.App.Browser;
using MossAgent.App.Services;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceViewModel : ObservableObject, IDisposable, IAsyncDisposable
{
    private readonly WorkspaceRunTracker _runs;
    private readonly WorkspaceTaskPanelCoordinator _panels;
    private readonly WorkspaceBackgroundRunCoordinator _backgroundRuns;
    private readonly WorkspaceAttachmentDraftCoordinator _attachmentDrafts;
    private readonly WorkspaceSendCoordinator _sender;
    private readonly WorkspaceLoadCoordinator _loads = new();
    private AiProvider? _selectedProvider;
    private ModelProfile? _selectedModel;
    private ApprovalPolicy _approvalPolicy = ApprovalPolicy.AskEveryTime;
    private string _activity = WorkspaceText.Get("ChatReady");
    private readonly WorkspaceSendPreparation _preparation = new();
    private bool _hasSendPreparationError;
    private bool _isShuttingDown;
    private bool _disposed;
    private readonly WorkspaceProjectPickerController _projectPicker;

    public WorkspaceViewModel(
        IConfigurationRepository configurations,
        IAgentConfigurationRepository agents,
        WorkspaceTaskFactory taskFactory,
        WorkspaceRunTracker runs,
        WorkspaceRunController runController,
        WorkspaceTaskPanelCoordinator panels,
        WorkspaceAttachmentManagerViewModel attachments,
        WorkspaceRepositoryStatusViewModel repositoryStatus,
        WorkspaceCatalogViewModel catalog,
        AppStartupViewModel? startup = null,
        ISidebarPreferenceStore? sidebarPreferences = null,
        WorkspaceFileChangeUndoService? undoService = null, WorkspaceFileChangeStatisticsService? statisticsService = null,
        IConversationPreferenceRepository? conversationPreferences = null, IConversationDraftRepository? conversationDrafts = null,
        WorkspaceFollowupServices? followups = null, IConversationSteeringRepository? steering = null,
        IWorktreeCreationIntents? creationIntents = null, IWorktreeCleanupIntents? cleanupIntents = null,
        IComposerPreferenceStore? composerPreferences = null)
    {
        _runs = runs;
        _panels = panels;
        Attachments = attachments;
        RepositoryStatus = repositoryStatus;
        Catalog = catalog;
        Drafts = new(conversationDrafts);
        _attachmentDrafts = new(attachments, Drafts);
        Shutdown = new() { Drafts = Drafts };
        Drafts.PropertyChanged += HandleDraftChanged;
        FileChanges = new WorkspaceFileChangesViewModel(catalog, panels, undoService, statisticsService);
        SidebarProjects = new WorkspaceSidebarProjectsViewModel(catalog.ProjectNavigation, sidebarPreferences);
        SidebarSearch = new WorkspaceSidebarSearchViewModel(catalog);
        _ = SidebarProjects.LoadAsync();
        Startup = startup ?? new AppStartupViewModel();
        Startup.PropertyChanged += HandleStartupChanged;
        Tabs = new WorkspaceConversationTabsViewModel(catalog);
        Catalog.ActiveMessagesProvider = _runs.GetMessages;
        Catalog.IsTaskRunning = taskId => _runs.IsRunning(taskId) || Queue?.IsPreparing(taskId) == true || FileChanges.Undo.OwnsTask(taskId);
        Catalog.SelectionChanged += HandleSelectionChanged;
        Catalog.Messages.CollectionChanged += HandleMessagesChanged;
        ModelPicker = new ModelPickerViewModel(configurations);
        AgentPicker = new WorkspaceAgentPickerViewModel(agents, ModelPicker);
        AgentPicker.PropertyChanged += HandleAgentPickerChanged;
        ModelPicker.ModelSelected += HandleModelSelected;
        ModelPicker.PropertyChanged += HandleModelPickerChanged;
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        PrimaryActionCommand = new RelayCommand(ExecutePrimaryAction, CanExecutePrimaryAction);
        _preparation.StateChanged += NotifyRunStateChanged;
        FileChanges.Undo.StateChanged += HandleUndoStateChanged;
        _projectPicker = new(catalog);
        _projectPicker.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(WorkspaceProjectPickerController.HasMatches)) OnPropertyChanged(nameof(ProjectPickerHasMatches));
        };
        OpenProjectPickerCommand = new RelayCommand(_projectPicker.Open);
        SetApprovalPolicyCommand = new RelayCommand<ApprovalPolicy>(policy => ApprovalPolicy = policy);
        Catalog.TaskManagement.PropertyChanged += HandleTaskMutationChanged;
        Catalog.WorktreeAuthorization.PropertyChanged += HandleTaskMutationChanged;
        ConversationPreferences = conversationPreferences is null ? null
            : new WorkspaceConversationPreferences(conversationPreferences, catalog, ModelPicker, AgentPicker);
        _sender = new(taskFactory, runs, runController, panels, catalog, attachments, Drafts, _preparation,
            ConversationPreferences, new(PresentEventAsync, SetActivity, ApplyTaskUpdate, NotifyRunStateChanged),
            () => _disposed, () => IsShuttingDown, value => Activity = value);
        Queue = followups?.Create(this, taskFactory, runs, runController, panels,
            new(PresentEventAsync, SetActivity, ApplyTaskUpdate, NotifyRunStateChanged), () => HasActiveRun && CanSend(true),
            () => !_disposed && Startup.IsReady && !IsRunning && !IsShuttingDown && !FileChanges.Undo.IsBusy
                && Catalog.SelectedTask is { } task && !Catalog.TaskManagement.BlocksTaskStart(task.Id)
                && !Catalog.WorktreeAuthorization.BlocksTaskStart(task.Id))
            ?? new(null, catalog, () => WorkspaceSendRequest.Capture(this), () => false, () => false, () => HasActiveRun, PrimaryActionCommand);
        WorktreeRecovery = new(creationIntents, cleanupIntents);
        Queue.StateChanged += NotifyRunStateChanged;
        Steering = new(steering, runs, runController, catalog, Drafts, PrimaryActionCommand, Queue.SubmitCommand,
            () => !_disposed && !IsShuttingDown && Startup.IsReady && !FileChanges.Undo.IsBusy);
        ComposerPreferences = new(composerPreferences, Queue.SubmitCommand, Steering.AlternateSubmitCommand);
        _backgroundRuns = new(taskFactory, runs, runController, panels, catalog,
            () => !_disposed && !IsShuttingDown && Startup.IsReady, PresentEventAsync, SetActivity,
            ApplyTaskUpdate, NotifyRunStateChanged, (taskId, preference) => ConversationPreferences?.Remember(taskId, preference));
        _ = LoadAsync();
    }

    public WorkspaceCatalogViewModel Catalog { get; }
    public WorkspaceSidebarProjectsViewModel SidebarProjects { get; }
    public WorkspaceSidebarSearchViewModel SidebarSearch { get; }
    public AppStartupViewModel Startup { get; }
    public WorkspaceConversationTabsViewModel Tabs { get; }
    public ModelPickerViewModel ModelPicker { get; }
    public WorkspaceAgentPickerViewModel AgentPicker { get; }
    public WorkspaceConversationPreferences? ConversationPreferences { get; }
    public WorkspaceComposerDraftsViewModel Drafts { get; }
    public WorkspaceFollowupQueueViewModel Queue { get; }
    public WorkspaceWorktreeRecoveryViewModel WorktreeRecovery { get; }
    public WorkspaceSteeringViewModel Steering { get; }
    public WorkspaceComposerPreferencesViewModel ComposerPreferences { get; }
    public IRelayCommand ComposerSubmitCommand => ComposerPreferences.SubmitCommand;
    public IAsyncRelayCommand ComposerAlternateSubmitCommand => ComposerPreferences.AlternateSubmitCommand;
    public AppShutdownViewModel Shutdown { get; }
    public bool IsShuttingDown => _isShuttingDown;
    public ObservableCollection<WorkspaceProjectNavigationViewModel> ProjectPickerProjects => _projectPicker.Projects;
    public IRelayCommand OpenProjectPickerCommand { get; }
    public string ProjectPickerQuery
    {
        get => _projectPicker.Query;
        set => _projectPicker.Query = value;
    }
    public bool ProjectPickerHasMatches => _projectPicker.HasMatches;
    public string ProjectPickerSearchPlaceholder => WorkspaceLabel(nameof(ProjectPickerSearchPlaceholder));
    public string ProjectPickerCreateLabel => WorkspaceLabel(nameof(ProjectPickerCreateLabel));
    public string ProjectPickerNoMatches => WorkspaceLabel(nameof(ProjectPickerNoMatches));
    public string ProjectPickerSwitchLabel => WorkspaceLabel(nameof(ProjectPickerSwitchLabel));
    public string ProjectlessChatLabel => WorkspaceLabel(nameof(ProjectlessChatLabel));
    public string EmptySessionPrefix => WorkspaceLabel(nameof(EmptySessionPrefix));
    public string EmptySessionSuffix => WorkspaceLabel(nameof(EmptySessionSuffix));
    public ObservableCollection<AgentProfile> Agents => AgentPicker.Items;
    public bool HasAgents => AgentPicker.HasAgents;
    public string SelectedAgentLabel => AgentPicker.SelectedLabel;
    public WorkspaceAttachmentManagerViewModel Attachments { get; }
    public WorkspaceRepositoryStatusViewModel RepositoryStatus { get; }
    public FilePreviewPaneViewModel FilePreview => _panels.Previews;
    public WorkspaceFileChangesViewModel FileChanges { get; }
    public ObservableCollection<ChatMessageViewModel> Messages => Catalog.Messages;
    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand PrimaryActionCommand { get; }
    public IRelayCommand<AgentProfile?> SelectAgentCommand => AgentPicker.SelectCommand;
    public IRelayCommand<ApprovalPolicy> SetApprovalPolicyCommand { get; }
    public bool IsRunning => _preparation.IsActive || (Catalog.SelectedTask is { } task && (_runs.IsRunning(task.Id) || Queue?.IsPreparing(task.Id) == true));
    public bool HasActiveRun => Catalog.SelectedTask is { } task && _runs.IsRunning(task.Id);
    public bool HasSendPreparationError { get => _hasSendPreparationError; private set => SetProperty(ref _hasSendPreparationError, value); }
    public string SendPreparationError => Catalog.Environment.PreparationNotice ?? WorkspaceLabel(nameof(SendPreparationError));
    public string PrimaryActionLabel => WorkspaceLabel(HasActiveRun ? "StopRunLabel" : IsRunning ? _preparation.IsCancellationRequested ? "CancellingPreparationLabel" : "CancelPreparationLabel" : "SendRunLabel");
    public bool IsSelectedTaskFailed => Catalog.SelectedTask?.Status == AgentTaskStatus.Failed;
    public string TaskFailureHint => WorkspaceFailureText.Get(Catalog.SelectedTask?.FailureReason);
    public bool HasMessages => Messages.Count > 0;
    public bool IsEmptySession => Messages.Count == 0;
    public bool IsProjectlessChat => Catalog.SelectedProject is null;
    public string EmptySessionTitle => WorkspaceLabel(nameof(EmptySessionTitle));
    public string ApprovalPolicyLabel => MossAgent.App.Resources.ApprovalPolicyText.Label(ApprovalPolicy);
    public bool IsReadOnlyApprovalPolicy => ApprovalPolicy == ApprovalPolicy.ReadOnly;
    public bool IsAskEveryTimeApprovalPolicy => ApprovalPolicy == ApprovalPolicy.AskEveryTime;
    public bool IsFullAccessApprovalPolicy => ApprovalPolicy == ApprovalPolicy.FullAccess;

    public AiProvider? SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            if (!SetProperty(ref _selectedProvider, value)) return;
            NotifyRunStateChanged();
        }
    }

    public ModelProfile? SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (!SetProperty(ref _selectedModel, value)) return;
            Attachments.SetModel(value);
            ModelPicker.SyncSelection(SelectedProvider, value);
            NotifyRunStateChanged();
        }
    }

    public AgentProfile? SelectedAgent
    {
        get => AgentPicker.SelectedProfile;
        set => AgentPicker.Select(value);
    }

    public ApprovalPolicy ApprovalPolicy
    {
        get => _approvalPolicy;
        set
        {
            if (SetProperty(ref _approvalPolicy, value))
            {
                FileChanges.Undo.SelectedApprovalPolicy = value;
                OnPropertyChanged(nameof(ApprovalPolicyLabel));
                OnPropertyChanged(nameof(IsReadOnlyApprovalPolicy));
                OnPropertyChanged(nameof(IsAskEveryTimeApprovalPolicy));
                OnPropertyChanged(nameof(IsFullAccessApprovalPolicy));
            }
        }
    }

    public string ComposerText
    {
        get => Drafts.Text;
        set => Drafts.Text = value;
    }

    public string Activity { get => _activity; private set => SetProperty(ref _activity, value); }
    public Task LoadAsync() => _loads.RunAsync(LoadCoreAsync);
    private async Task LoadCoreAsync()
    {
        if (_disposed) return;
        await Catalog.ReloadAsync();
        if (_disposed) return;
        await Drafts.LoadCurrentAsync();
        _projectPicker.Refresh();
        OnPropertyChanged(nameof(IsProjectlessChat));
        await ModelPicker.ReloadAsync();
        if (_disposed) return;
        await ReloadAgentsAsync();
        if (_disposed) return;
        await ComposerPreferences.LoadAsync();
        await RepositoryStatus.RefreshAsync(Catalog.SelectedProject, Catalog.SelectedTask);
    }

    public async Task ReloadAgentsAsync()
    {
        await AgentPicker.ReloadAsync();
        if (!_disposed) ConversationPreferences?.ReapplySelection();
    }

    public Task<AgentTask> RunBackgroundAsync(ProjectProfile project, AiProvider provider,
        ModelProfile model, AutomationSchedule schedule, Guid taskId, CancellationToken cancellationToken) =>
        _backgroundRuns.RunAsync(project, provider, model, schedule, taskId, cancellationToken);

    private void HandleModelSelected(AiProvider? provider, ModelProfile? model)
    {
        SelectedProvider = provider;
        SelectedModel = model;
    }

    private void ExecutePrimaryAction()
    {
        if (IsRunning) Stop();
        else if (CanSend()) _preparation.Start(SendAsync);
    }

    private bool CanExecutePrimaryAction() => !_disposed && (IsRunning ? CanStop() : CanSend());
    private bool CanSend(bool allowRunning = false) =>
        !_disposed && Startup.IsReady && Drafts.CanSend && Attachments.IsModelCompatible && !_preparation.IsActive && !IsShuttingDown && !FileChanges.Undo.IsBusy && AgentPicker.CanUseSelection
        && SelectedProvider is not null
        && SelectedModel is not null && ModelPicker.CanSendWithSelectedModel
        && ModelPicker.SelectedItem?.Provider.Id == SelectedProvider.Id
        && ModelPicker.SelectedItem.Model.Id == SelectedModel.Id && (!string.IsNullOrWhiteSpace(ComposerText) || Attachments.HasImageItems)
        && (Catalog.SelectedTask is null || (Catalog.SelectedTask.ProjectId == Catalog.SelectedProject?.Id && (allowRunning || !_runs.IsRunning(Catalog.SelectedTask.Id))
            && Queue?.IsPreparing(Catalog.SelectedTask.Id) != true
            && !Catalog.TaskManagement.BlocksTaskStart(Catalog.SelectedTask.Id)
            && !Catalog.WorktreeAuthorization.BlocksTaskStart(Catalog.SelectedTask.Id)));

    private void HandleTaskMutationChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(WorkspaceTaskManagementViewModel.IsBusy))
        {
            PrimaryActionCommand.NotifyCanExecuteChanged();
            Queue?.NotifyStateChanged();
            Catalog.TaskManagement.NotifyTaskRunStateChanged();
        }
    }

    private void HandleStartupChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(AppStartupViewModel.IsReady))
        {
            PrimaryActionCommand.NotifyCanExecuteChanged();
            if (Startup.IsReady) _ = WorktreeRecovery.RefreshAsync();
        }
    }

    private void HandleModelPickerChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ModelPickerViewModel.CanSendWithSelectedModel))
            PrimaryActionCommand.NotifyCanExecuteChanged();
    }

    private void HandleAgentPickerChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(WorkspaceAgentPickerViewModel.SelectedProfile)) OnPropertyChanged(nameof(SelectedAgent));
        if (args.PropertyName == nameof(WorkspaceAgentPickerViewModel.SelectedLabel)) OnPropertyChanged(nameof(SelectedAgentLabel));
        if (args.PropertyName == nameof(WorkspaceAgentPickerViewModel.HasAgents)) OnPropertyChanged(nameof(HasAgents));
        if (args.PropertyName == nameof(WorkspaceAgentPickerViewModel.CanUseSelection)) PrimaryActionCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Steering.Dispose();
        ComposerPreferences.Dispose();
        Queue.StateChanged -= NotifyRunStateChanged;
        Queue.Dispose();
        _preparation.Dispose();
        RepositoryStatus.Dispose();
        Tabs.Dispose();
        Catalog.SelectionChanged -= HandleSelectionChanged;
        Catalog.Messages.CollectionChanged -= HandleMessagesChanged;
        Catalog.TaskManagement.PropertyChanged -= HandleTaskMutationChanged;
        Catalog.WorktreeAuthorization.PropertyChanged -= HandleTaskMutationChanged;
        Catalog.WorktreeAuthorization.Dispose();
        WorktreeRecovery.Dispose();
        Startup.PropertyChanged -= HandleStartupChanged;
        ModelPicker.PropertyChanged -= HandleModelPickerChanged;
        ModelPicker.ModelSelected -= HandleModelSelected;
        AgentPicker.PropertyChanged -= HandleAgentPickerChanged;
        ConversationPreferences?.Dispose();
        Drafts.PropertyChanged -= HandleDraftChanged;
        _attachmentDrafts.Dispose();
        Drafts.Dispose();
        AgentPicker.Dispose();
        ModelPicker.Dispose();
        Catalog.ProjectEditor.Dispose();
        Catalog.TaskManagement.Dispose();
        SidebarProjects.Dispose();
        SidebarSearch.Dispose();
        Attachments.Dispose();
        FileChanges.Undo.StateChanged -= HandleUndoStateChanged;
        FileChanges.Dispose();
        FilePreview.Dispose();
    }

    public Task ShutdownAsync() => Shutdown.RunCleanupAsync(StopAllAsync);

    public async ValueTask DisposeAsync()
    {
        _isShuttingDown = true; await Queue.StopAsync(); await Steering.StopAsync(); await _preparation.StopAsync(); await ComposerPreferences.StopAsync();
        await Catalog.WorktreeAuthorization.StopAsync(); await WorktreeRecovery.StopAsync();
        await Task.WhenAll(RepositoryStatus.StopAsync(), FileChanges.Statistics.StopAsync(), SidebarProjects.StopAsync());
        if (ConversationPreferences is not null) await ConversationPreferences.StopAsync();
        await Drafts.StopAsync();
        Dispose();
    }

    private async Task StopAllAsync()
    {
        _isShuttingDown = true;
        Attachments.Stop();
        OnPropertyChanged(nameof(IsShuttingDown));
        NotifyRunStateChanged();
        _runs.CancelAll();
        await Catalog.WorktreeAuthorization.StopAsync(); await WorktreeRecovery.StopAsync();
        try { await Queue.StopAsync(); await Steering.StopAsync(); await _preparation.StopAsync(); await ComposerPreferences.StopAsync(); }
        finally
        {
            var navigationStop = Task.WhenAll(RepositoryStatus.StopAsync(), SidebarProjects.StopAsync());
            try
            {
                await _runs.StopAsync();
                await FileChanges.StopAsync();
                if (ConversationPreferences is not null) await ConversationPreferences.StopAsync();
                await Drafts.StopAsync();
            }
            finally { await navigationStop; }
        }
    }

    private async Task SendAsync(CancellationToken cancellationToken)
    {
        var request = WorkspaceSendRequest.Capture(this);
        Catalog.Environment.ClearPreparationNotice();
        HasSendPreparationError = false;
        try
        {
            await _sender.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!_disposed) Activity = WorkspaceText.Get("PreparationCancelled");
        }
        catch (Exception exception)
        {
            if (_disposed) throw new InvalidOperationException("Task finalization failed.");
            Catalog.Environment.ReportPreparationFailure(exception);
            OnPropertyChanged(nameof(SendPreparationError));
            HasSendPreparationError = true;
            Activity = SendPreparationError;
            if (IsShuttingDown) throw new InvalidOperationException("Task finalization failed.");
        }
    }

    private async Task PresentEventAsync(WorkspaceRunState state, AgentEvent agentEvent) =>
        state.Assistant = await WorkspaceAgentEventPresenter.PresentAsync(
            agentEvent, state.Assistant, state.Messages,
            status => SetActivity(state, status),
            presented => CompleteEventPresentation(state, presented));

    private void CompleteEventPresentation(WorkspaceRunState state, AgentEvent agentEvent)
    {
        Steering.NotifyRunEvent(state.Task.Id, agentEvent);
        if (agentEvent is AgentToolEvent { ToolName: "git.diff", Result.Content: { } diff }
            && Catalog.SelectedTask?.Id == state.Task.Id)
        {
            _panels.ShowDiff(diff);
        }
        Catalog.ShowActiveMessages(state.Task.Id);
    }

    private void HandleSelectionChanged()
    {
        WorktreeRecovery.SetProject(Catalog.SelectedProject);
        if (_attachmentDrafts.SelectContext(Catalog.SelectedProject, Catalog.SelectedTask?.Id, preserveItems: _preparation.IsActive) && !_preparation.IsActive)
            HasSendPreparationError = false;
        _projectPicker.Refresh();
        _ = RepositoryStatus.RefreshAsync(Catalog.SelectedProject, Catalog.SelectedTask);
        OnPropertyChanged(nameof(IsSelectedTaskFailed));
        OnPropertyChanged(nameof(TaskFailureHint));
        OnPropertyChanged(nameof(EmptySessionTitle));
        OnPropertyChanged(nameof(IsProjectlessChat));
        var task = Catalog.SelectedTask;
        if (task is null || task.ProjectId != Catalog.SelectedProject?.Id)
        {
            _panels.ClearSelection();
            Activity = WorkspaceText.Get("ChatReady");
        }
        else
        {
            ApprovalPolicy = task.ApprovalPolicy;
            _panels.Attach(task, Catalog.SelectedProject);
            Activity = _runs.TryGet(task.Id, out var state) ? state!.Activity
                : string.Format(WorkspaceText.Get("TaskOpenedActivity"), task.Title);
        }
        NotifyRunStateChanged();
    }

    private void HandleDraftChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(WorkspaceComposerDraftsViewModel.Text)) OnPropertyChanged(nameof(ComposerText));
        if (args.PropertyName == nameof(WorkspaceComposerDraftsViewModel.CanSend)) NotifyRunStateChanged();
    }

    private static string WorkspaceLabel(string key) => WorkspaceText.Get(key);

    private void HandleMessagesChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        OnPropertyChanged(nameof(HasMessages));
        OnPropertyChanged(nameof(IsEmptySession));
    }

    private void SetActivity(WorkspaceRunState state, string status)
    {
        state.Activity = status;
        if (Catalog.SelectedTask?.Id == state.Task.Id) Activity = status;
    }

    private void ApplyTaskUpdate(AgentTask task) =>
        Catalog.UpsertTask(task, Catalog.SelectedTask?.Id == task.Id);

    private bool CanStop() => HasActiveRun || _preparation.CanCancel || Catalog.SelectedTask is { } task && Queue.IsPreparing(task.Id);
    private void Stop()
    {
        if (HasActiveRun && Catalog.SelectedTask is { } task) _runs.Cancel(task.Id);
        else _preparation.Cancel();
        if (Catalog.SelectedTask is { } selected) Queue.CancelPreparation(selected.Id);
    }

    private void HandleUndoStateChanged()
    {
        PrimaryActionCommand.NotifyCanExecuteChanged();
        Queue?.NotifyStateChanged();
        Catalog.NotifyTaskRunStateChanged();
    }

    private void NotifyRunStateChanged()
    {
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(HasActiveRun));
        OnPropertyChanged(nameof(PrimaryActionLabel));
        OnPropertyChanged(nameof(HasMessages));
        OnPropertyChanged(nameof(IsEmptySession));
        Catalog.IsLocked = _preparation.IsActive || IsShuttingDown;
        Catalog.NotifyTaskRunStateChanged();
        PrimaryActionCommand.NotifyCanExecuteChanged();
    }
}
