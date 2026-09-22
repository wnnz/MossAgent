using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Agent;
using MossAgent.Application.Artifacts;
using MossAgent.Application.Models;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceViewModel : ObservableObject
{
    private readonly IWorkspaceRepository _workspaces;
    private readonly IConfigurationRepository _configurations;
    private readonly IAgentRunner _agent;
    private readonly ITaskArtifactPaths _artifacts;
    private readonly ITaskBrowserSessionManager _browserSessions;
    private readonly WorkspaceTaskFactory _taskFactory;
    private readonly TerminalPaneViewModel _terminal;
    private readonly DiffPaneViewModel _diff;
    private readonly AuditPaneViewModel _audit;
    private AiProvider? _selectedProvider;
    private ModelProfile? _selectedModel;
    private ApprovalPolicy _approvalPolicy = ApprovalPolicy.AskEveryTime;
    private string _composerText = string.Empty;
    private string _activity = "添加或选择项目后即可创建任务。";
    private bool _useWorktree;
    private CancellationTokenSource? _runCancellation;

    public WorkspaceViewModel(
        IWorkspaceRepository workspaces,
        IConfigurationRepository configurations,
        IAgentRunner agent,
        ITaskArtifactPaths artifacts,
        ITaskBrowserSessionManager browserSessions,
        WorkspaceTaskFactory taskFactory,
        WorkspaceCatalogViewModel catalog,
        TerminalPaneViewModel terminal,
        DiffPaneViewModel diff,
        AuditPaneViewModel audit)
    {
        _workspaces = workspaces;
        _configurations = configurations;
        _agent = agent;
        _artifacts = artifacts;
        _browserSessions = browserSessions;
        _taskFactory = taskFactory;
        _terminal = terminal;
        _diff = diff;
        _audit = audit;
        Catalog = catalog;
        Catalog.SelectionChanged += HandleSelectionChanged;
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        SendCommand = new AsyncRelayCommand(SendAsync, CanSend);
        StopCommand = new RelayCommand(Stop, () => _runCancellation is not null);
        _ = LoadAsync();
    }

    public WorkspaceCatalogViewModel Catalog { get; }
    public ObservableCollection<AiProvider> Providers { get; } = [];
    public ObservableCollection<ModelProfile> Models { get; } = [];
    public ObservableCollection<ChatMessageViewModel> Messages => Catalog.Messages;
    public IReadOnlyList<ApprovalPolicy> ApprovalPolicies { get; } = Enum.GetValues<ApprovalPolicy>();
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand SendCommand { get; }
    public IRelayCommand StopCommand { get; }

    public AiProvider? SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            if (SetProperty(ref _selectedProvider, value))
            {
                _ = LoadModelsAsync();
                SendCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public ModelProfile? SelectedModel
    {
        get => _selectedModel;
        set { SetProperty(ref _selectedModel, value); SendCommand.NotifyCanExecuteChanged(); }
    }

    public ApprovalPolicy ApprovalPolicy { get => _approvalPolicy; set => SetProperty(ref _approvalPolicy, value); }
    public string ComposerText { get => _composerText; set { SetProperty(ref _composerText, value); SendCommand.NotifyCanExecuteChanged(); } }
    public string Activity { get => _activity; private set => SetProperty(ref _activity, value); }
    public bool UseWorktree { get => _useWorktree; set => SetProperty(ref _useWorktree, value); }

    private async Task LoadAsync()
    {
        await Catalog.ReloadAsync();
        var providers = await _configurations.GetProvidersAsync(CancellationToken.None);
        Providers.ReplaceWith(providers.Where(static provider => provider.IsEnabled));
        SelectedProvider = Providers.FirstOrDefault(static provider => provider.IsDefault)
            ?? Providers.FirstOrDefault();
    }
    private async Task LoadModelsAsync()
    {
        Models.Clear();
        if (SelectedProvider is null)
        {
            return;
        }

        var models = await _configurations.GetModelsAsync(SelectedProvider.Id, CancellationToken.None);
        Models.ReplaceWith(models.Where(static model => model.IsEnabled));
        SelectedModel = Models.FirstOrDefault(static model => model.IsDefault) ?? Models.FirstOrDefault();
    }
    private bool CanSend() =>
        _runCancellation is null
        && Catalog.SelectedProject is not null
        && SelectedProvider is not null
        && SelectedModel is not null
        && !string.IsNullOrWhiteSpace(ComposerText);

    private async Task SendAsync()
    {
        var prompt = ComposerText.Trim();
        ComposerText = string.Empty;
        AgentTask? task = null;
        _runCancellation = new CancellationTokenSource();
        NotifyRunStateChanged();

        try
        {
            task = await PrepareTaskAsync(prompt);
            AttachTaskPanels(task);
            var assistant = new ChatMessageViewModel("MossAgent", string.Empty);
            Messages.Add(new ChatMessageViewModel("你", prompt));
            Messages.Add(assistant);
            await RunAgentAsync(task, assistant, _runCancellation.Token);
        }
        catch (OperationCanceledException) when (task is not null)
        {
            Activity = "任务已取消。";
            ApplyTaskUpdate(await _taskFactory.SetStatusAsync(
                task, AgentTaskStatus.Cancelled, CancellationToken.None));
        }
        catch (Exception exception)
        {
            Activity = $"任务失败：{exception.Message}";
            if (task is not null)
            {
                ApplyTaskUpdate(await _taskFactory.SetStatusAsync(
                    task, AgentTaskStatus.Failed, CancellationToken.None));
            }
        }
        finally
        {
            _runCancellation.Dispose();
            _runCancellation = null;
            NotifyRunStateChanged();
        }
    }
    private async Task<AgentTask> PrepareTaskAsync(string prompt)
    {
        var task = Catalog.SelectedTask is null
            ? await _taskFactory.CreateAsync(
                Catalog.SelectedProject!, prompt, ApprovalPolicy,
                UseWorktree, CancellationToken.None)
            : await _taskFactory.ResumeAsync(
                Catalog.SelectedTask, prompt, ApprovalPolicy, CancellationToken.None);
        if (Catalog.SelectedTask is null)
        {
            Messages.Clear();
        }

        Catalog.UpsertTask(task);
        return task;
    }

    private async Task RunAgentAsync(
        AgentTask task,
        ChatMessageViewModel assistant,
        CancellationToken cancellationToken)
    {
        var context = CreateToolContext(task);
        var history = await _workspaces.GetMessagesAsync(task.Id, cancellationToken);
        var request = new AgentRunRequest(
            SelectedProvider!, SelectedModel!,
            history.Select(WorkspaceConversationMapper.ToModelMessage).ToArray(), context);
        var failed = false;

        await foreach (var agentEvent in _agent.RunAsync(request, cancellationToken))
        {
            failed |= agentEvent is AgentFailureEvent;
            if (agentEvent is AgentToolEvent { ToolName: "git.diff", Result.Content: { } diff })
            {
                _diff.Show(diff);
            }

            await WorkspaceAgentEventPresenter.PresentAsync(
                agentEvent, assistant, Messages, status => Activity = status);
        }

        ApplyTaskUpdate(await _taskFactory.CompleteAsync(
            task, assistant.Content,
            failed ? AgentTaskStatus.Failed : AgentTaskStatus.Completed,
            cancellationToken));
    }

    private void HandleSelectionChanged()
    {
        SendCommand.NotifyCanExecuteChanged();
        var task = Catalog.SelectedTask;
        if (task is null)
        {
            Activity = "输入内容即可创建新任务。";
            return;
        }

        ApprovalPolicy = task.ApprovalPolicy;
        AttachTaskPanels(task);
        Activity = $"已打开任务：{task.Title}";
    }

    private void AttachTaskPanels(AgentTask task)
    {
        var directory = task.WorktreePath ?? Catalog.SelectedProject!.PrimaryDirectory;
        _terminal.AttachTask(task.Id, directory);
        _diff.Attach(CreateToolContext(task));
        _audit.Attach(task.Id);
    }

    private ToolExecutionContext CreateToolContext(AgentTask task) =>
        new(
            task.Id,
            task.WorktreePath ?? Catalog.SelectedProject!.PrimaryDirectory,
            _taskFactory.GetAuthorizedRoots(Catalog.SelectedProject!, task),
            task.ApprovalPolicy,
            ArtifactDirectory: _artifacts.GetTaskDirectory(task.Id),
            BrowserSession: _browserSessions.GetLazySession(task.Id));

    private void ApplyTaskUpdate(AgentTask task) => Catalog.UpsertTask(task);
    private void Stop() => _runCancellation?.Cancel();

    private void NotifyRunStateChanged()
    {
        Catalog.IsLocked = _runCancellation is not null;
        SendCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
    }
}
