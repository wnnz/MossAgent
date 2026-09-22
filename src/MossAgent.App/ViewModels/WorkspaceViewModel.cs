using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Agent;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

/// <summary>
/// 工作区核心视图模型，负责项目选择、会话执行、模型切换及消息输入。
/// </summary>
public sealed class WorkspaceViewModel : ObservableObject
{
    private readonly IConfigurationRepository _configurations;
    private readonly WorkspaceTaskFactory _taskFactory;
    private readonly WorkspaceRunTracker _runs;
    private readonly WorkspaceRunController _runController;
    private readonly WorkspaceTaskPanelCoordinator _panels;
    private AiProvider? _selectedProvider;
    private ModelProfile? _selectedModel;
    private ApprovalPolicy _approvalPolicy = ApprovalPolicy.AskEveryTime;
    private string _composerText = string.Empty;
    private string _activity = "添加或选择项目后即可创建任务。";
    private bool _useWorktree;
    private bool _isStarting;

    public WorkspaceViewModel(
        IConfigurationRepository configurations,
        WorkspaceTaskFactory taskFactory,
        WorkspaceRunTracker runs,
        WorkspaceRunController runController,
        WorkspaceTaskPanelCoordinator panels,
        WorkspaceCatalogViewModel catalog)
    {
        _configurations = configurations;
        _taskFactory = taskFactory;
        _runs = runs;
        _runController = runController;
        _panels = panels;
        Catalog = catalog;
        Catalog.ActiveMessagesProvider = _runs.GetMessages;
        Catalog.IsTaskRunning = _runs.IsRunning;
        Catalog.SelectionChanged += HandleSelectionChanged;
        ModelPicker = new ModelPickerViewModel(_configurations);
        ModelPicker.ModelSelected += (p, m) => { SelectedProvider = p; SelectedModel = m; };
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        SendCommand = new AsyncRelayCommand(SendAsync, CanSend);
        StopCommand = new RelayCommand(Stop, CanStop);
        PrimaryActionCommand = new RelayCommand(ExecutePrimaryAction, CanExecutePrimaryAction);
        ToggleApprovalPolicyCommand = new RelayCommand(ToggleApprovalPolicy);
        _ = LoadAsync();
    }

    public WorkspaceCatalogViewModel Catalog { get; }
    public ModelPickerViewModel ModelPicker { get; }
    public ObservableCollection<AiProvider> Providers { get; } = [];
    public ObservableCollection<ModelProfile> Models { get; } = [];
    public ObservableCollection<ChatMessageViewModel> Messages => Catalog.Messages;
    public IReadOnlyList<ApprovalPolicy> ApprovalPolicies { get; } = Enum.GetValues<ApprovalPolicy>();
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand SendCommand { get; }
    public IRelayCommand StopCommand { get; }
    public IRelayCommand PrimaryActionCommand { get; }
    public IRelayCommand ToggleApprovalPolicyCommand { get; }
    public bool IsRunning => _isStarting || (Catalog.SelectedTask is { } task && _runs.IsRunning(task.Id));
    public bool HasMessages => Messages.Count > 0;
    public bool IsEmptySession => Messages.Count == 0;
    public string ApprovalPolicyLabel => ApprovalPolicy switch
    {
        ApprovalPolicy.FullAccess => "⚠️ 完全访问",
        ApprovalPolicy.AskEveryTime => "🛡️ 每次询问",
        ApprovalPolicy.ReadOnly => "🔒 只读模式",
        _ => ApprovalPolicy.ToString()
    };

    public AiProvider? SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            if (SetProperty(ref _selectedProvider, value))
            {
                _ = LoadModelsAsync();
                NotifyRunStateChanged();
            }
        }
    }

    public ModelProfile? SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (SetProperty(ref _selectedModel, value))
            {
                ModelPicker.SyncSelection(SelectedProvider, value);
                NotifyRunStateChanged();
            }
        }
    }

    public ApprovalPolicy ApprovalPolicy
    {
        get => _approvalPolicy;
        set
        {
            if (SetProperty(ref _approvalPolicy, value))
            {
                OnPropertyChanged(nameof(ApprovalPolicyLabel));
            }
        }
    }

    public string ComposerText
    {
        get => _composerText;
        set
        {
            if (SetProperty(ref _composerText, value)) NotifyRunStateChanged();
        }
    }

    public string Activity { get => _activity; private set => SetProperty(ref _activity, value); }
    public bool UseWorktree { get => _useWorktree; set => SetProperty(ref _useWorktree, value); }

    public async Task LoadAsync()
    {
        await Catalog.ReloadAsync();
        var providers = await _configurations.GetProvidersAsync(CancellationToken.None);
        Providers.ReplaceWith(providers.Where(static p => p.IsEnabled));
        SelectedProvider = Providers.FirstOrDefault(static p => p.IsDefault) ?? Providers.FirstOrDefault();
        await ModelPicker.ReloadAsync();
        ModelPicker.SyncSelection(SelectedProvider, SelectedModel);
    }

    public async Task LoadModelsAsync()
    {
        Models.Clear();
        if (SelectedProvider is null) return;
        var models = await _configurations.GetModelsAsync(SelectedProvider.Id, CancellationToken.None);
        Models.ReplaceWith(models.Where(static m => m.IsEnabled));
        SelectedModel = Models.FirstOrDefault(static m => m.IsDefault) ?? Models.FirstOrDefault();
        ModelPicker.SyncSelection(SelectedProvider, SelectedModel);
    }

    private void ExecutePrimaryAction()
    {
        if (IsRunning) Stop();
        else if (CanSend()) _ = SendAsync();
    }

    private bool CanExecutePrimaryAction() => IsRunning ? CanStop() : CanSend();

    private bool CanSend() =>
        !_isStarting && Catalog.SelectedProject is not null && SelectedProvider is not null
        && SelectedModel is not null && !string.IsNullOrWhiteSpace(ComposerText)
        && (Catalog.SelectedTask is null || !_runs.IsRunning(Catalog.SelectedTask.Id));

    private async Task SendAsync()
    {
        var prompt = ComposerText.Trim();
        var project = Catalog.SelectedProject!;
        var existingTask = Catalog.SelectedTask;
        var provider = SelectedProvider!;
        var model = SelectedModel!;
        var visibleMessages = existingTask is null ? [] : Messages.ToList();
        ComposerText = string.Empty;
        _isStarting = true;
        NotifyRunStateChanged();
        try
        {
            var task = await PrepareTaskAsync(project, existingTask, prompt);
            var assistant = new ChatMessageViewModel("MossAgent", string.Empty);
            var messages = new ObservableCollection<ChatMessageViewModel>(visibleMessages)
            {
                new("你", prompt),
                assistant
            };
            var state = new WorkspaceRunState(task, project, provider, model, messages, assistant);
            _runs.Add(state);
            Catalog.UpsertTask(task);
            Catalog.ShowActiveMessages(task.Id);
            _panels.Attach(task, project);
            _ = _runController.RunAsync(
                state, _panels, PresentEventAsync, SetActivity,
                ApplyTaskUpdate, NotifyRunStateChanged);
        }
        catch (Exception exception)
        {
            Activity = $"无法启动任务：{exception.Message}";
        }
        finally
        {
            _isStarting = false;
            NotifyRunStateChanged();
        }
    }

    private Task<AgentTask> PrepareTaskAsync(ProjectProfile project, AgentTask? task, string prompt) =>
        task is null
            ? _taskFactory.CreateAsync(project, prompt, ApprovalPolicy, UseWorktree, CancellationToken.None)
            : _taskFactory.ResumeAsync(task, prompt, ApprovalPolicy, CancellationToken.None);

    private async Task PresentEventAsync(WorkspaceRunState state, AgentEvent agentEvent) =>
        await WorkspaceAgentEventPresenter.PresentAsync(
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
        var task = Catalog.SelectedTask;
        if (task is null) Activity = "输入内容即可创建新任务。";
        else
        {
            ApprovalPolicy = task.ApprovalPolicy;
            _panels.Attach(task, Catalog.SelectedProject!);
            Activity = _runs.TryGet(task.Id, out var state) ? state!.Activity : $"已打开任务：{task.Title}";
        }
        NotifyRunStateChanged();
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

    private void ToggleApprovalPolicy()
    {
        ApprovalPolicy = ApprovalPolicy switch
        {
            ApprovalPolicy.AskEveryTime => ApprovalPolicy.FullAccess,
            ApprovalPolicy.FullAccess => ApprovalPolicy.ReadOnly,
            ApprovalPolicy.ReadOnly => ApprovalPolicy.AskEveryTime,
            _ => ApprovalPolicy.AskEveryTime
        };
    }

    private void NotifyRunStateChanged()
    {
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(HasMessages));
        OnPropertyChanged(nameof(IsEmptySession));
        Catalog.IsLocked = _isStarting;
        Catalog.NotifyTaskRunStateChanged();
        SendCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        PrimaryActionCommand.NotifyCanExecuteChanged();
    }
}
