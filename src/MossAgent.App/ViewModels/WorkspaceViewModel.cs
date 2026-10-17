using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Agent;
using MossAgent.Application.Persistence;
using MossAgent.Application.Settings;
using MossAgent.Domain;
using MossAgent.App.Browser;

using MossAgent.App.Services;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceViewModel : ObservableObject, IDisposable, IAsyncDisposable
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.App.Resources.WorkspaceStrings", typeof(WorkspaceViewModel).Assembly);
    private readonly WorkspaceTaskFactory _taskFactory;
    private readonly WorkspaceRunTracker _runs;
    private readonly WorkspaceRunController _runController;
    private readonly WorkspaceTaskPanelCoordinator _panels;
    private readonly WorkspaceBackgroundRunCoordinator _backgroundRuns;
    private AiProvider? _selectedProvider;
    private ModelProfile? _selectedModel;
    private ApprovalPolicy _approvalPolicy = ApprovalPolicy.AskEveryTime;
    private string _composerText = string.Empty;
    private string _projectPickerQuery = string.Empty;
    private string _activity = WorkspaceText.Get("ChatReady");
    private bool _isStarting;
    private bool _hasSendPreparationError;
    private bool _isShuttingDown;
    private bool _disposed;
    private Task? _startingTask;
    private Guid? _composerProjectId;
    private Guid? _composerTaskId;

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
        IConversationPreferenceRepository? conversationPreferences = null)
    {
        _taskFactory = taskFactory;
        _runs = runs;
        _runController = runController;
        _panels = panels;
        Attachments = attachments;
        RepositoryStatus = repositoryStatus;
        Catalog = catalog;
        FileChanges = new WorkspaceFileChangesViewModel(catalog, panels, undoService, statisticsService);
        SidebarProjects = new WorkspaceSidebarProjectsViewModel(catalog.ProjectNavigation, sidebarPreferences);
        SidebarSearch = new WorkspaceSidebarSearchViewModel(catalog);
        _ = SidebarProjects.LoadAsync();
        Startup = startup ?? new AppStartupViewModel();
        Startup.PropertyChanged += HandleStartupChanged;
        Tabs = new WorkspaceConversationTabsViewModel(catalog);
        Catalog.ActiveMessagesProvider = _runs.GetMessages;
        Catalog.IsTaskRunning = taskId => _runs.IsRunning(taskId) || FileChanges.Undo.OwnsTask(taskId);
        Catalog.SelectionChanged += HandleSelectionChanged;
        Catalog.Messages.CollectionChanged += HandleMessagesChanged;
        ModelPicker = new ModelPickerViewModel(configurations);
        AgentPicker = new WorkspaceAgentPickerViewModel(agents, ModelPicker);
        AgentPicker.PropertyChanged += HandleAgentPickerChanged;
        ModelPicker.ModelSelected += HandleModelSelected;
        ModelPicker.PropertyChanged += HandleModelPickerChanged;
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        PrimaryActionCommand = new RelayCommand(ExecutePrimaryAction, CanExecutePrimaryAction);
        FileChanges.Undo.StateChanged += HandleUndoStateChanged;
        OpenProjectPickerCommand = new RelayCommand(OpenProjectPicker);
        SetApprovalPolicyCommand = new RelayCommand<ApprovalPolicy>(SetApprovalPolicy);
        Catalog.TaskManagement.PropertyChanged += HandleTaskMutationChanged;
        ConversationPreferences = conversationPreferences is null ? null
            : new WorkspaceConversationPreferences(conversationPreferences, catalog, ModelPicker, AgentPicker);
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
    public AppShutdownViewModel Shutdown { get; } = new();
    public bool IsShuttingDown => _isShuttingDown;
    public ObservableCollection<WorkspaceProjectNavigationViewModel> ProjectPickerProjects { get; } = [];
    public IRelayCommand OpenProjectPickerCommand { get; }
    public string ProjectPickerQuery
    {
        get => _projectPickerQuery;
        set { if (SetProperty(ref _projectPickerQuery, value)) RefreshProjectPicker(); }
    }
    public bool ProjectPickerHasMatches => ProjectPickerProjects.Count > 0;
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
    public bool IsRunning => _isStarting || (Catalog.SelectedTask is { } task && _runs.IsRunning(task.Id));
    public bool HasActiveRun => Catalog.SelectedTask is { } task && _runs.IsRunning(task.Id);
    public bool HasSendPreparationError { get => _hasSendPreparationError; private set => SetProperty(ref _hasSendPreparationError, value); }
    public string SendPreparationError => WorkspaceLabel(nameof(SendPreparationError));
    public string PrimaryActionLabel => WorkspaceLabel(HasActiveRun ? "StopRunLabel" : _isStarting ? "PreparingRunLabel" : "SendRunLabel");
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
        get => _composerText;
        set { if (SetProperty(ref _composerText, value)) NotifyRunStateChanged(); }
    }

    public string Activity { get => _activity; private set => SetProperty(ref _activity, value); }
    public async Task LoadAsync()
    {
        if (_disposed) return;
        await Catalog.ReloadAsync();
        if (_disposed) return;
        RefreshProjectPicker();
        OnPropertyChanged(nameof(IsProjectlessChat));
        await ModelPicker.ReloadAsync();
        if (_disposed) return;
        await ReloadAgentsAsync();
        if (_disposed) return;
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
        else if (CanSend()) _startingTask = SendAsync();
    }

    private bool CanExecutePrimaryAction() => !_disposed && (IsRunning ? CanStop() : CanSend());
    private bool CanSend() =>
        !_disposed && Startup.IsReady && !_isStarting && !IsShuttingDown && !FileChanges.Undo.IsBusy && AgentPicker.CanUseSelection
        && SelectedProvider is not null
        && SelectedModel is not null && ModelPicker.CanSendWithSelectedModel
        && ModelPicker.SelectedItem?.Provider.Id == SelectedProvider.Id
        && ModelPicker.SelectedItem.Model.Id == SelectedModel.Id && !string.IsNullOrWhiteSpace(ComposerText)
        && (Catalog.SelectedTask is null || (Catalog.SelectedTask.ProjectId == Catalog.SelectedProject?.Id && !_runs.IsRunning(Catalog.SelectedTask.Id)
            && !Catalog.TaskManagement.BlocksTaskStart(Catalog.SelectedTask.Id)));

    private void HandleTaskMutationChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(WorkspaceTaskManagementViewModel.IsBusy))
            PrimaryActionCommand.NotifyCanExecuteChanged();
    }

    private void HandleStartupChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(AppStartupViewModel.IsReady)) PrimaryActionCommand.NotifyCanExecuteChanged();
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
        RepositoryStatus.Dispose();
        Tabs.Dispose();
        Catalog.SelectionChanged -= HandleSelectionChanged;
        Catalog.Messages.CollectionChanged -= HandleMessagesChanged;
        Catalog.TaskManagement.PropertyChanged -= HandleTaskMutationChanged;
        Startup.PropertyChanged -= HandleStartupChanged;
        ModelPicker.PropertyChanged -= HandleModelPickerChanged;
        ModelPicker.ModelSelected -= HandleModelSelected;
        AgentPicker.PropertyChanged -= HandleAgentPickerChanged;
        ConversationPreferences?.Dispose();
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
        await Task.WhenAll(RepositoryStatus.StopAsync(), FileChanges.Statistics.StopAsync(), SidebarProjects.StopAsync());
        if (ConversationPreferences is not null) await ConversationPreferences.StopAsync();
        Dispose();
    }

    private async Task StopAllAsync()
    {
        _isShuttingDown = true;
        OnPropertyChanged(nameof(IsShuttingDown));
        NotifyRunStateChanged();
        _runs.CancelAll();
        try { if (_startingTask is not null) await _startingTask; }
        finally
        {
            var navigationStop = Task.WhenAll(RepositoryStatus.StopAsync(), SidebarProjects.StopAsync());
            try
            {
                await _runs.StopAsync();
                await FileChanges.StopAsync();
                if (ConversationPreferences is not null) await ConversationPreferences.StopAsync();
            }
            finally { await navigationStop; }
        }
    }

    private async Task SendAsync()
    {
        var draft = ComposerText;
        var prompt = draft.Trim();
        var project = Catalog.SelectedProject;
        var existingTask = Catalog.SelectedTask;
        var preference = ConversationPreferences?.Capture();
        var provider = SelectedProvider!;
        var model = SelectedModel! with { ReasoningEffort = ModelPicker.EffectiveReasoningEffort };
        var agentInstructions = SelectedAgent?.SystemPrompt;
        var attachmentContext = Attachments.BuildContext();
        var capturedAttachments = Attachments.Items.ToArray();
        var messageAttachments = capturedAttachments
            .Select(static attachment => new ChatMessageAttachmentViewModel(
                attachment.DisplayPath, attachment.SizeLabel))
            .ToArray();
        var visibleMessages = existingTask is null ? [] : Messages.ToList();
        HasSendPreparationError = false;
        _isStarting = true;
        NotifyRunStateChanged();
        try
        {
            var task = await PrepareTaskAsync(project, existingTask, prompt);
            ConversationPreferences?.Remember(task.Id, preference);
            if (ComposerText == draft) ComposerText = string.Empty;
            if (IsShuttingDown)
            {
                Catalog.UpsertTask(await _taskFactory.SetStatusAsync(task, AgentTaskStatus.Cancelled, CancellationToken.None));
                return;
            }
            var assistant = new ChatMessageViewModel("MossAgent", string.Empty);
            var messages = new ObservableCollection<ChatMessageViewModel>(visibleMessages)
                { new("你", prompt, attachments: messageAttachments, createdAt: task.UpdatedAt), assistant };
            var state = new WorkspaceRunState(
                task, project, provider, model, messages, assistant,
                attachmentContext, agentInstructions);
            Attachments.Consume(capturedAttachments);
            _runs.Add(state);
            Catalog.UpsertTask(task);
            Catalog.ShowActiveMessages(task.Id);
            _panels.Attach(task, project);
            _runs.TrackExecution(() => _runController.RunAsync(
                state, _panels, PresentEventAsync, SetActivity,
                ApplyTaskUpdate, NotifyRunStateChanged));
        }
        catch (Exception)
        {
            HasSendPreparationError = true;
            Activity = SendPreparationError;
            if (IsShuttingDown) throw new InvalidOperationException("Task finalization failed.");
        }
        finally
        {
            _isStarting = false;
            NotifyRunStateChanged();
        }
    }

    private Task<AgentTask> PrepareTaskAsync(ProjectProfile? project, AgentTask? task, string prompt) =>
        task is null
            ? _taskFactory.CreateAsync(project, prompt, ApprovalPolicy, false, CancellationToken.None)
            : _taskFactory.ResumeAsync(task, prompt, project is null ? ApprovalPolicy.ReadOnly : ApprovalPolicy, CancellationToken.None);

    private async Task PresentEventAsync(WorkspaceRunState state, AgentEvent agentEvent) =>
        state.Assistant = await WorkspaceAgentEventPresenter.PresentAsync(
            agentEvent, state.Assistant, state.Messages,
            status => SetActivity(state, status),
            presented => CompleteEventPresentation(state, presented));

    private void CompleteEventPresentation(WorkspaceRunState state, AgentEvent agentEvent)
    {
        if (agentEvent is AgentToolEvent { ToolName: "git.diff", Result.Content: { } diff }
            && Catalog.SelectedTask?.Id == state.Task.Id)
        {
            _panels.ShowDiff(diff);
        }
        Catalog.ShowActiveMessages(state.Task.Id);
    }

    private void HandleSelectionChanged()
    {
        var projectId = Catalog.SelectedProject?.Id;
        var taskId = Catalog.SelectedTask?.Id;
        if (!_isStarting && (projectId != _composerProjectId || taskId != _composerTaskId))
        {
            HasSendPreparationError = false;
            Attachments.Clear();
        }
        _composerProjectId = projectId;
        _composerTaskId = taskId;
        RefreshProjectPicker();
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
            Activity = _runs.TryGet(task.Id, out var state) ? state!.Activity : $"已打开任务：{task.Title}";
        }
        NotifyRunStateChanged();
    }

    private void OpenProjectPicker()
    {
        ProjectPickerQuery = string.Empty;
        RefreshProjectPicker();
    }

    private void RefreshProjectPicker()
    {
        var query = ProjectPickerQuery.Trim();
        ProjectPickerProjects.ReplaceWith(Catalog.ProjectNavigation.Where(group =>
            group.Project.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)));
        OnPropertyChanged(nameof(ProjectPickerHasMatches));
    }

    private static string WorkspaceLabel(string key) => Labels.GetString(key)
        ?? throw new InvalidOperationException($"Missing workspace resource: {key}");

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

    private bool CanStop() => Catalog.SelectedTask is { } task && _runs.IsRunning(task.Id);
    private void Stop()
    {
        if (Catalog.SelectedTask is { } task) _runs.Cancel(task.Id);
    }

    private void SetApprovalPolicy(ApprovalPolicy policy) => ApprovalPolicy = policy;

    private void HandleUndoStateChanged()
    {
        PrimaryActionCommand.NotifyCanExecuteChanged();
        Catalog.NotifyTaskRunStateChanged();
    }

    private void NotifyRunStateChanged()
    {
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(HasActiveRun));
        OnPropertyChanged(nameof(PrimaryActionLabel));
        OnPropertyChanged(nameof(HasMessages));
        OnPropertyChanged(nameof(IsEmptySession));
        Catalog.IsLocked = _isStarting || IsShuttingDown;
        Catalog.NotifyTaskRunStateChanged();
        PrimaryActionCommand.NotifyCanExecuteChanged();
    }
}
