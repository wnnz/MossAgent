using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Application.Automations;
using MossAgent.Application.Settings;
using MossAgent.Application.Attachments;
using MossAgent.App.Services;
using MossAgent.Application.SourceControl;
using Xunit;

namespace MossAgent.App.Tests;

internal sealed class WorkspaceModelSelectionFixture : IAsyncDisposable
{
    private readonly CancellationTokenSource _timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
    private ServiceProvider? _services;
    private ModelSelectionResponseServer? _server;
    private WorkspaceViewModel? _workspace;
    private bool _disposed;

    private WorkspaceModelSelectionFixture() => _timeout.CancelAfter(TimeSpan.FromSeconds(30));

    public MainWindowViewModel Main => _services!.GetRequiredService<MainWindowViewModel>();
    public WorkspaceViewModel Workspace => Main.Workspace;
    public ModelSelectionResponseServer Server => _server!;
    public IConfigurationRepository Configurations => _services!.GetRequiredService<IConfigurationRepository>();
    public IAgentConfigurationRepository AgentProfiles => _services!.GetRequiredService<IAgentConfigurationRepository>();
    public IWorkspaceRepository Tasks => _services!.GetRequiredService<IWorkspaceRepository>();
    public IConversationDraftRepository Drafts => _services!.GetRequiredService<IConversationDraftRepository>();
    public IConversationFollowupRepository Followups => _services!.GetRequiredService<IConversationFollowupRepository>();
    public IConversationSteeringRepository Steering => _services!.GetRequiredService<IConversationSteeringRepository>();
    public ISourceAccountRepository SourceAccounts => _services!.GetRequiredService<ISourceAccountRepository>();
    public ISourceRepositoryGrants SourceGrants => _services!.GetRequiredService<ISourceRepositoryGrants>();
    public ISourceReviewChats SourceChats => _services!.GetRequiredService<ISourceReviewChats>();
    public IConversationAttachmentStore AttachmentStore => _services!.GetRequiredService<IConversationAttachmentStore>();
    public WorkspaceFollowupServices FollowupServices(IConversationFollowupRepository repository,
        IConversationAttachmentStore? preparationAttachments = null) => new(repository,
        preparationAttachments is null ? _services!.GetRequiredService<MossAgent.Application.Conversations.ConversationFollowupPreparer>()
            : ActivatorUtilities.CreateInstance<MossAgent.Application.Conversations.ConversationFollowupPreparer>(_services!, preparationAttachments),
        _services!.GetRequiredService<ConversationAttachmentThumbnailFactory>());
    public WorkspaceRunTracker Runs => _services!.GetRequiredService<WorkspaceRunTracker>();
    public AutomationScheduler Scheduler => _services!.GetRequiredService<AutomationScheduler>();
    public IAutomationRepository Automations => _services!.GetRequiredService<IAutomationRepository>();
    public MossAgent.Infrastructure.Git.GitProcessRunner Git => _services!.GetRequiredService<MossAgent.Infrastructure.Git.GitProcessRunner>();
    public MossAgent.Application.Git.IGitWorktreeService Worktrees => _services!.GetRequiredService<MossAgent.Application.Git.IGitWorktreeService>();
    public WorkspaceTaskPanelCoordinator Panels => _services!.GetRequiredService<WorkspaceTaskPanelCoordinator>();
    public MossAgent.Tools.Abstractions.IToolExecutor Tools => _services!.GetRequiredService<MossAgent.Tools.Abstractions.IToolExecutor>();
    public MossAgent.Tools.Abstractions.IToolExecutionAuditReader ToolAudits => _services!.GetRequiredService<MossAgent.Tools.Abstractions.IToolExecutionAuditReader>();
    public ModelProfile SelectedModel { get; private set; } = null!;
    public ModelProfile AlternativeModel { get; private set; } = null!;
    public AiProvider SelectedProvider { get; private set; } = null!;
    public CancellationToken Token => _timeout.Token;
    public string DataRoot => Path.Combine(_root, "data");

    public WorkspaceViewModel CreateWorkspace(IWorkspaceRepository repository, WorkspaceRunTracker runs,
        IAgentConfigurationRepository? agents = null, IWorkspaceRepository? catalogRepository = null,
        IConversationPreferenceRepository? conversationPreferences = null, ISidebarPreferenceStore? sidebarPreferences = null,
        IFilePickerService? attachmentPicker = null, IWorkspaceAttachmentReader? attachmentReader = null,
        IConversationDraftRepository? conversationDrafts = null, IConversationAttachmentStore? attachmentStore = null,
        ISelectedAttachmentReader? selectedAttachmentReader = null, WorkspaceFollowupServices? followups = null,
        Action<WorkspaceRunController>? configureRunController = null)
    {
        var services = _services!;
        attachmentStore ??= services.GetRequiredService<IConversationAttachmentStore>();
        var factory = new WorkspaceTaskFactory(repository, services.GetRequiredService<MossAgent.Application.Git.IGitWorktreeService>(), attachmentStore);
        var executor = new WorkspaceRunExecutor(repository, services.GetRequiredService<MossAgent.Application.Agent.IAgentRunner>(), factory, attachmentStore);
        var controller = new WorkspaceRunController(executor, runs, factory);
        configureRunController?.Invoke(controller);
        var catalog = catalogRepository is null
            ? ActivatorUtilities.CreateInstance<WorkspaceCatalogViewModel>(services)
            : ActivatorUtilities.CreateInstance<WorkspaceCatalogViewModel>(services, catalogRepository);
        var attachments = new WorkspaceAttachmentManagerViewModel(
            attachmentPicker ?? services.GetRequiredService<IFilePickerService>(),
            attachmentReader ?? services.GetRequiredService<IWorkspaceAttachmentReader>(),
            selectedReader: selectedAttachmentReader ?? services.GetRequiredService<ISelectedAttachmentReader>(),
            thumbnailRenderer: services.GetRequiredService<IAttachmentThumbnailRenderer>());
        var repositoryStatus = ActivatorUtilities.CreateInstance<WorkspaceRepositoryStatusViewModel>(services);
        var arguments = new List<object> { factory, runs, controller, catalog, attachments, repositoryStatus, agents ?? AgentProfiles };
        if (conversationPreferences is not null) arguments.Add(conversationPreferences);
        if (sidebarPreferences is not null) arguments.Add(sidebarPreferences);
        arguments.Add(conversationDrafts ?? new TestConversationDraftRepository());
        if (followups is not null) arguments.Add(followups);
        return ActivatorUtilities.CreateInstance<WorkspaceViewModel>(services, arguments.ToArray());
    }

    public static async Task<WorkspaceModelSelectionFixture> CreateAsync(bool pauseResponse = false, bool withProject = true,
        Action<IServiceCollection>? configure = null)
    {
        var fixture = new WorkspaceModelSelectionFixture();
        try
        {
            Directory.CreateDirectory(fixture._root);
            var services = AppComposition.CreateServiceCollection(Path.Combine(fixture._root, "data"));
            configure?.Invoke(services);
            fixture._services = services.BuildServiceProvider(validateScopes: true);
            await fixture._services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
            fixture._server = new ModelSelectionResponseServer(fixture.Token, pauseResponse);
            fixture._workspace = fixture.Workspace;
            fixture.SelectedProvider = await fixture.AddProviderAsync("selected", true);
            fixture.SelectedModel = await fixture.AddModelAsync(fixture.SelectedProvider, "selected-model", true);
            var alternative = await fixture.AddProviderAsync("alternative", false);
            fixture.AlternativeModel = await fixture.AddModelAsync(alternative, "alternative-model", false);
            if (withProject)
                await fixture._services.GetRequiredService<WorkspaceProjectService>().CreateAsync("Selection fixture", [fixture._root]);
            await fixture.Workspace.LoadAsync();
            fixture.Workspace.ComposerText = "合成请求，验证明确选择的模型。";
            Assert.Equal(fixture.SelectedModel.Id, fixture.Workspace.SelectedModel?.Id);
            Assert.True(fixture.Workspace.PrimaryActionCommand.CanExecute(null));
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    private async Task<AiProvider> AddProviderAsync(string name, bool isDefault)
    {
        var providers = Main.Providers;
        await providers.RefreshCommand.ExecuteAsync(null);
        providers.OpenCreateFormCommand.Execute(null);
        providers.Name = name;
        providers.BaseUrl = Server.BaseUri.AbsoluteUri;
        providers.ApiKey = "synthetic-fixture-key";
        providers.Protocol = ProviderProtocol.OpenAiResponses;
        providers.IsDefault = isDefault;
        providers.IsEnabled = true;
        await providers.SaveCommand.ExecuteAsync(null);
        Assert.False(providers.HasValidationError, providers.Status);
        return Assert.Single(providers.Items, provider => provider.Name == name);
    }

    private async Task<ModelProfile> AddModelAsync(AiProvider provider, string id, bool isDefault)
    {
        var models = Main.Models;
        await models.RefreshProvidersCommand.ExecuteAsync(null);
        models.SelectedProvider = Assert.Single(models.Providers, item => item.Id == provider.Id);
        models.NewModelCommand.Execute(null);
        models.ModelId = id;
        models.DisplayName = id;
        models.ContextLength = 32000;
        models.MaximumOutputTokens = 1024;
        models.ReasoningEffort = "medium";
        models.IsDefault = isDefault;
        models.IsEnabled = true;
        await models.SaveModelCommand.ExecuteAsync(null);
        Assert.False(models.HasStatusError, models.Status);
        return Assert.Single(await Configurations.GetModelsAsync(provider.Id, Token));
    }

    public async Task<AutomationSchedule> AuthorizedAsync(AutomationSchedule schedule, long generation = 1)
    {
        var project = Assert.Single(await Tasks.GetProjectsAsync(Token), item => item.Id == schedule.ProjectId);
        var provider = Assert.Single(await Configurations.GetProvidersAsync(Token), item => item.Id == schedule.ProviderId);
        var model = Assert.Single(await Configurations.GetModelsAsync(provider.Id, Token), item => item.Id == schedule.ModelId);
        return schedule with { AuthorizationSnapshot = AutomationAuthorizationSnapshot.Capture(
            project, provider, model, schedule.Prompt, schedule.ApprovalPolicy, generation, DateTimeOffset.UtcNow) };
    }

    public async Task SetSelectedModelEnabledAsync(bool enabled)
    {
        Main.Models.EditProviderModelCommand.Execute(SelectedModel);
        Main.Models.IsEnabled = enabled;
        await Main.Models.SaveModelCommand.ExecuteAsync(null);
        Assert.False(Main.Models.HasStatusError, Main.Models.Status);
    }

    public async Task WaitUntilAsync(Func<bool> predicate)
    {
        while (!predicate()) await Task.Delay(15, Token);
    }

    public async Task AssertPersistedAsync(AgentTaskStatus status)
    {
        var task = Assert.IsType<AgentTask>(Workspace.Catalog.SelectedTask);
        var stored = Assert.Single(await Tasks.GetTasksAsync(task.ProjectId, Token));
        Assert.Equal(status, stored.Status);
        var messages = await Tasks.GetMessagesAsync(task.Id, Token);
        Assert.Contains(messages, message => message.Role == MessageRole.User);
        if (status == AgentTaskStatus.Completed)
            Assert.Contains(messages, message => message.Content.Contains("模型选择链路已完成。", StringComparison.Ordinal));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (_services is not null) await Scheduler.StopAsync();
        if (_workspace is { IsRunning: true } workspace)
        {
            workspace.PrimaryActionCommand.Execute(null);
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (workspace.IsRunning) await Task.Delay(15, cleanup.Token);
        }
        if (_server is not null) await _server.DisposeAsync();
        if (_services is not null) await _services.DisposeAsync();
        await _timeout.CancelAsync();
        for (var attempt = 0; attempt < 12 && Directory.Exists(_root); attempt++)
        {
            using var connection = new SqliteConnectionFactory(new AppDataPaths(Path.Combine(_root, "data"))).Create();
            SqliteConnection.ClearPool(connection);
            try { Directory.Delete(_root, recursive: true); }
            catch (IOException) when (attempt < 11) { await Task.Delay(50, CancellationToken.None); }
            catch (UnauthorizedAccessException) when (attempt < 11) { await Task.Delay(50, CancellationToken.None); }
        }
        _timeout.Dispose();
    }
}
