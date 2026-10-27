using Microsoft.Extensions.DependencyInjection;
using MossAgent.Application.Persistence;
using MossAgent.Application.Conversations;
using MossAgent.Browser;
using MossAgent.Browser.Tools;
using MossAgent.Mcp;
using MossAgent.Application.Models;
using MossAgent.Application.Agent;
using MossAgent.Application.Artifacts;
using MossAgent.Application.Attachments;
using MossAgent.Application.Networking;
using MossAgent.App.ViewModels;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Infrastructure.Networking;
using MossAgent.Infrastructure.Git;
using MossAgent.Infrastructure.Attachments;
using MossAgent.Application.Git;
using MossAgent.Application.Settings;
using MossAgent.Providers;
using MossAgent.Providers.OpenAi;
using MossAgent.Providers.Anthropic;
using MossAgent.Providers.Gemini;
using MossAgent.Agent;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;
using MossAgent.Tools.Abstractions.Browser;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Processes;
using MossAgent.Tools.BuiltIn.Git;
using MossAgent.Platform.Windows.Capture;
using MossAgent.Platform.Windows.Windowing;
using MossAgent.Platform.Windows.Terminal;
using MossAgent.Platform.Windows.Processes;
using MossAgent.Tools.Abstractions.Processes;
using MossAgent.Tools.Abstractions.Terminal;
using MossAgent.App.Browser;
using MossAgent.App.Services;
using MossAgent.Application.Automations;
using MossAgent.Tools.Abstractions.Files;
using MossAgent.Platform.Windows.Files;
using MossAgent.Application.SourceControl;
using MossAgent.Infrastructure.SourceControl;
using MossAgent.Tools.Abstractions.SourceControl;
using MossAgent.Tools.BuiltIn.SourceControl;
using MossAgent.Application.Authorization;
using MossAgent.Application.Plugins;
using MossAgent.Infrastructure.Plugins;

namespace MossAgent.App;

internal static class AppComposition
{
    public static ServiceProvider CreateServices(string? dataRoot = null) =>
        CreateServiceCollection(dataRoot).BuildServiceProvider(validateScopes: true);

    internal static IServiceCollection CreateServiceCollection(string? dataRoot = null)
    {
        var services = new ServiceCollection();
        var resolvedDataRoot = string.IsNullOrWhiteSpace(dataRoot)
            ? null
            : Path.GetFullPath(dataRoot);
        RegisterInfrastructure(services, resolvedDataRoot);
        RegisterFileAndAgentTools(services);
        RegisterBrowserAndDesktopTools(services);
        RegisterAgentRuntime(services);
        RegisterWorkspace(services);
        return services;
    }

    private static void RegisterInfrastructure(IServiceCollection services, string? dataRoot)
    {
        services.AddSingleton(new AppDataPaths(dataRoot));
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<IDatabaseInitializer, SqliteDatabaseInitializer>();
        services.AddSingleton<IConfigurationRepository, SqliteConfigurationRepository>();
        services.AddSingleton<ISubagentConfigurationRepository, SqliteSubagentConfigurationRepository>();
        services.AddSingleton<IAgentConfigurationRepository, SqliteAgentConfigurationRepository>();
        services.AddSingleton<IBrowserConfigurationRepository, SqliteBrowserConfigurationRepository>();
        services.AddSingleton<IMcpConfigurationRepository, SqliteMcpConfigurationRepository>();
        services.AddSingleton<ISourceAccountRepository, SqliteSourceAccountRepository>();
        services.AddSingleton<ISourceAccountHttpClientFactory, SourceAccountHttpClientFactory>();
        services.AddSingleton<ISourceAccountIdentityClient, GitHubIdentityClient>();
        services.AddSingleton<SourceAccountConnections>();
        services.AddSingleton<ISourceRepositoryGrants, SqliteSourceRepositoryGrants>();
        services.AddSingleton<ISourceReviewSessions, SqliteSourceReviewSessions>();
        services.AddSingleton<IPullRequestMarks, SqlitePullRequestMarks>();
        services.AddSingleton<IPullRequestClient, GitHubPullRequestClient>();
        services.AddSingleton<ISourceReviewReader, SourceReviewReader>();
        services.AddSingleton<SourceRepositoryAccess>();
        services.AddSingleton<ISourceReviewChats, SqliteSourceReviewChats>();
        services.AddSingleton<SourceReviewChatCreator>();
        services.AddSingleton<SourceReviewChatAccess>();
        services.AddSingleton<IPullRequestInspectionClient, GitHubPullRequestInspectionClient>();
        services.AddSingleton<ISourcePullRequestInspection, SourcePullRequestInspection>();
        services.AddSingleton<IPullRequestWriteClient, GitHubPullRequestWriteClient>();
        services.AddSingleton<ISourcePullRequestWriter, PullRequestWriter>();
        services.AddSingleton<IPullRequestBranchClient, GitHubPullRequestBranchClient>();
        services.AddSingleton<ISourcePullRequestBranches, SourcePullRequestBranches>();
        services.AddSingleton<IPullRequestInboxClient, GitHubPullRequestInboxClient>();
        services.AddSingleton<SourceInboxReader>();
        services.AddSingleton<IWorkspaceRepository, SqliteWorkspaceRepository>();
        services.AddSingleton<IProjectAuthorizationChanges, ProjectAuthorizationChanges>();
        services.AddSingleton<IProjectAuthorizationReader>(static provider =>
            provider.GetRequiredService<IWorkspaceRepository>() as IProjectAuthorizationReader
            ?? new WorkspaceProjectAuthorizationReader(provider.GetRequiredService<IWorkspaceRepository>()));
        services.AddSingleton<ProjectAuthorizationMonitor>();
        services.AddSingleton<IConversationPreferenceRepository, SqliteConversationPreferenceRepository>();
        services.AddSingleton<IConversationDraftRepository, SqliteConversationDraftRepository>();
        services.AddSingleton<IGitCommitDraftRepository, SqliteGitCommitDraftRepository>();
        services.AddSingleton<IConversationFollowupRepository, SqliteConversationFollowupRepository>();
        services.AddSingleton<IConversationSteeringRepository, SqliteConversationSteeringRepository>();
        services.AddSingleton<ConversationFollowupPreparer>();
        services.AddSingleton<WorkspaceFollowupServices>();
        services.AddSingleton<IAutomationRepository, SqliteAutomationRepository>();
        services.AddSingleton<IAutomationLease, AutomationSchedulerLease>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ISidebarPreferenceStore, JsonSidebarPreferenceStore>();
        services.AddSingleton<IComposerPreferenceStore, JsonComposerPreferenceStore>();
        services.AddSingleton<IAttachmentImageDecoder, SkiaAttachmentImageDecoder>();
        services.AddSingleton<IAttachmentDocumentTextExtractor, PdfTextExtractor>();
        services.AddSingleton<IAttachmentThumbnailRenderer, SkiaAttachmentThumbnailRenderer>();
        services.AddSingleton<WorkspaceAttachmentReader>();
        services.AddSingleton<IWorkspaceAttachmentReader>(static provider => provider.GetRequiredService<WorkspaceAttachmentReader>());
        services.AddSingleton<ISelectedAttachmentReader>(static provider => provider.GetRequiredService<WorkspaceAttachmentReader>());
        services.AddSingleton<IConversationAttachmentStore, ConversationAttachmentStore>();
        services.AddSingleton<AttachmentSnapshotLeases>();
        services.AddSingleton<AttachmentSnapshotReaper>();
        services.AddSingleton<IAttachmentSnapshotReaper>(
            static provider => provider.GetRequiredService<AttachmentSnapshotReaper>());
        services.AddSingleton<ITaskArtifactPaths, TaskArtifactPaths>();
        services.AddSingleton<IArtifactSessionJournal, SqliteArtifactSessionJournal>();
        services.AddSingleton<ISubagentRunJournal, SqliteSubagentRunJournal>();
        services.AddSingleton<IBrowserSessionJournal, SqliteBrowserSessionJournal>();
        services.AddSingleton<IPluginPackagePreviewer, ZipPluginPackagePreviewer>();
        services.AddSingleton<IInstalledPluginStore, SqliteInstalledPluginStore>();
        services.AddSingleton<IPluginPackageInstaller>(static provider => new PluginPackageInstaller(
            provider.GetRequiredService<IPluginPackagePreviewer>(),
            provider.GetRequiredService<IInstalledPluginStore>(),
            provider.GetRequiredService<AppDataPaths>().PluginsPath,
            provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<ITaskArtifactContentReader, TaskArtifactContentReader>();
        services.AddSingleton<IBrowserProfilePaths, BrowserProfilePaths>();
        services.AddSingleton<GitProcessRunner>();
        services.AddSingleton<IGitWorktreeService, GitWorktreeService>();
        services.AddSingleton<IWorktreeCreationIntents>(
            static provider => (GitWorktreeService)provider.GetRequiredService<IGitWorktreeService>());
        services.AddSingleton<IWorktreeCleanupIntents>(
            static provider => (GitWorktreeService)provider.GetRequiredService<IGitWorktreeService>());
        services.AddSingleton<IGitWorktreeAuthorizationService>(
            static provider => (GitWorktreeService)provider.GetRequiredService<IGitWorktreeService>());
        services.AddSingleton<IGitWorktreeReferenceDiscovery, GitWorktreeReferenceDiscovery>();
        services.AddSingleton<IGitRepositoryInfoService, GitRepositoryInfoService>();
        services.AddSingleton<MossAgent.Tools.Abstractions.Git.IGitRepositoryAuthorization, GitRepositoryAuthorization>();
        services.AddSingleton<IProviderHttpClientFactory, ProviderHttpClientFactory>();
        services.AddSingleton<IProxyConnectionTester, ProxyConnectionTester>();
        services.AddSingleton<IToolExecutionAuditSink, SqliteToolExecutionAuditSink>();
        services.AddSingleton<IToolExecutionAuditReader, SqliteToolExecutionAuditReader>();
        services.AddSingleton<IModelDiscoveryService, ModelDiscoveryService>();
        services.AddSingleton<OpenAiResponsesProvider>();
        services.AddSingleton<IModelProvider>(static provider => provider.GetRequiredService<OpenAiResponsesProvider>());
        services.AddSingleton<IModelProvider, OpenAiCompatibleProvider>();
        services.AddSingleton<IModelProvider, AnthropicMessagesProvider>();
        services.AddSingleton<IModelProvider, GeminiGenerateContentProvider>();
        services.AddSingleton<IModelProviderRegistry, ModelProviderRegistry>();
    }

    private static void RegisterFileAndAgentTools(IServiceCollection services)
    {
        services.AddSingleton<AuthorizedPathResolver>();
        services.AddSingleton<IFileReadHandleValidator, WindowsFileReadHandleValidator>();
        services.AddSingleton<IRootIdentitySessionFactory, WindowsRootIdentitySessionFactory>();
        services.AddSingleton<IWorktreeTargetClaimFactory, WindowsWorktreeTargetClaimFactory>();
        services.AddSingleton<IWorktreeRemovalGuardFactory, WindowsWorktreeRemovalGuardFactory>();
        services.AddSingleton<IAtomicFileWriteSessionFactory, WindowsAtomicFileWriteSessionFactory>();
        services.AddSingleton<IPathDeleteSessionFactory, WindowsPathDeleteSessionFactory>();
        services.AddSingleton<IPathMoveSessionFactory, WindowsPathMoveSessionFactory>();
        services.AddSingleton<IFileTreeReadSessionFactory, WindowsFileTreeReadSessionFactory>();
        services.AddSingleton<IAuthorizedDirectoryCreator, WindowsAuthorizedDirectoryCreator>();
        services.AddSingleton<IArtifactFileSessionFactory, WindowsArtifactFileSessionFactory>();
        services.AddSingleton<IDiffInputFileSessionFactory, WindowsDiffInputFileSessionFactory>();
        services.AddSingleton<IAuthorizedPathResolver>(
            static provider => provider.GetRequiredService<AuthorizedPathResolver>());
        services.AddSingleton<GitCommandRunner>();
        services.AddSingleton<GitIndexPaths>();
        services.AddSingleton<GitHunkApplier>();
        services.AddSingleton<GitCommitStateReader>();
        services.AddSingleton<GitPushEndpoints>();
        services.AddSingleton<GitPushStateReader>();
        services.AddSingleton<GitRemoteStateReader>();
        services.AddSingleton<GitRemoteUrlReader>();
        services.AddSingleton<GitRemoteUrlTarget>();
        services.AddSingleton<GitBranchUpstreamReader>();
        services.AddSingleton<IGitPushDestinationAuthorization, GitPushDestinationAuthorization>();
        services.AddSingleton<IBrowserSessionFactory, PlaywrightBrowserSessionFactory>();
        services.AddSingleton<EmbeddedBrowserWorkspaceViewModel>();
        services.AddSingleton<IEmbeddedBrowserSessionFactory, EmbeddedBrowserSessionFactory>();
        services.AddSingleton<BrowserProfileApprovalViewModel>();
        services.AddSingleton<IBrowserProfileAccessApproval>(
            static provider => provider.GetRequiredService<BrowserProfileApprovalViewModel>());
        services.AddSingleton<TaskBrowserSessionManager>();
        services.AddSingleton<ITaskBrowserSessionManager>(
            static provider => provider.GetRequiredService<TaskBrowserSessionManager>());
        services.AddSingleton<IBrowserTakeoverController>(
            static provider => provider.GetRequiredService<TaskBrowserSessionManager>());
        services.AddSingleton<BrowserTakeoverViewModel>();
        services.AddSingleton<ITerminalSessionManager, PtyTerminalSessionManager>();
        services.AddSingleton<ITaskProcessLauncher, WindowsTaskProcessLauncher>();
        services.AddSingleton<IAgentTool, FileReadTool>();
        services.AddSingleton<IAgentTool, FilePreviewTool>();
        services.AddSingleton<IAgentTool, FileChangeReviewTool>();
        services.AddSingleton<IAgentTool, FileChangeStatisticsTool>();
        services.AddSingleton<IAgentTool, FileChangeRestoreTool>();
        services.AddSingleton<IAgentTool, FileChangeRemoveCreatedTool>();
        services.AddSingleton<IFileRevealService, WindowsFileRevealService>();
        services.AddSingleton<IAgentTool, FileRevealTool>();
        services.AddSingleton<AtomicTextFileWriter>();
        services.AddSingleton<IAgentTool, DirectoryListTool>();
        services.AddSingleton<IAgentTool, FileWriteTool>();
        services.AddSingleton<IAgentTool, TextReplaceTool>();
        services.AddSingleton<IAgentTool, SearchTextTool>();
        services.AddSingleton<IAgentTool, FileFindTool>();
        services.AddSingleton<IAgentTool, DirectoryCreateTool>();
        services.AddSingleton<IAgentTool, PathCopyTool>();
        services.AddSingleton<IAgentTool, PathMoveTool>();
        services.AddSingleton<IAgentTool, PathDeleteTool>();
        services.AddSingleton<IAgentTool, ShellRunTool>();
        services.AddSingleton<IAgentTool, GitStatusTool>();
        services.AddSingleton<IAgentTool, GitDiffTool>();
        services.AddSingleton<IAgentTool, GitStageFileTool>();
        services.AddSingleton<IAgentTool, GitUnstageFileTool>();
        services.AddSingleton<IAgentTool, GitStagePathsTool>();
        services.AddSingleton<IAgentTool, GitUnstagePathsTool>();
        services.AddSingleton<IAgentTool, GitStageHunksTool>();
        services.AddSingleton<IAgentTool, GitUnstageHunksTool>();
        services.AddSingleton<IAgentTool, GitCommitPreviewTool>();
        services.AddSingleton<IAgentTool, GitCommitTool>();
        services.AddSingleton<IAgentTool, GitPushPreviewTool>();
        services.AddSingleton<IAgentTool, GitPushTool>();
        services.AddSingleton<IAgentTool, GitRemotesTool>();
        services.AddSingleton<IAgentTool, GitRemoteSetUrlTool>();
        services.AddSingleton<IAgentTool, GitLsRemoteTool>();
        services.AddSingleton<IAgentTool, GitBranchUpstreamTool>();
        services.AddSingleton<IAgentTool, GitLogTool>();
        services.AddSingleton<IAgentTool, GitCreateWorktreeTool>();
        services.AddSingleton<IAgentTool, SubagentTool>();
        services.AddSingleton<IAgentTool, PullRequestListTool>();
        services.AddSingleton<IAgentTool, PullRequestGetTool>();
        services.AddSingleton<IAgentTool, PullRequestFilesTool>();
        services.AddSingleton<IAgentTool, PullRequestCommentsTool>();
        services.AddSingleton<IAgentTool, PullRequestChecksTool>();
        services.AddSingleton<IAgentTool, PullRequestReviewsTool>();
        services.AddSingleton<IAgentTool, PullRequestActivityTool>();
        services.AddSingleton<IAgentTool, PullRequestCommentCreateTool>();
        services.AddSingleton<IAgentTool, PullRequestReviewSubmitTool>();
        services.AddSingleton<IAgentTool, PullRequestCloseTool>();
        services.AddSingleton<IAgentTool, PullRequestMergeTool>();
        services.AddSingleton<IAgentTool, PullRequestCreateTool>();
    }

    private static void RegisterBrowserAndDesktopTools(IServiceCollection services)
    {
        services.AddSingleton<IAgentTool, BrowserNavigateTool>();
        services.AddSingleton<IAgentTool, BrowserTabsTool>();
        services.AddSingleton<IAgentTool, BrowserNewTabTool>();
        services.AddSingleton<IAgentTool, BrowserSwitchTabTool>();
        services.AddSingleton<IAgentTool, BrowserCloseTabTool>();
        services.AddSingleton<IAgentTool, BrowserBackTool>();
        services.AddSingleton<IAgentTool, BrowserForwardTool>();
        services.AddSingleton<IAgentTool, BrowserReloadTool>();
        services.AddSingleton<IAgentTool, BrowserDevToolsTool>();
        services.AddSingleton<IAgentTool, BrowserSnapshotTool>();
        services.AddSingleton<IAgentTool, BrowserClickTool>();
        services.AddSingleton<IAgentTool, BrowserTypeTool>();
        services.AddSingleton<IAgentTool, BrowserWaitTool>();
        services.AddSingleton<IAgentTool, BrowserSelectTool>();
        services.AddSingleton<IAgentTool, BrowserScrollTool>();
        services.AddSingleton<IAgentTool, BrowserScreenshotTool>();
        services.AddSingleton<IAgentTool, BrowserDownloadTool>();
        services.AddSingleton<IAgentTool, ScreenCaptureTool>();
        services.AddSingleton<WindowCornerService>();
        services.AddSingleton<IAgentTool, TerminalStartTool>();
        services.AddSingleton<IAgentTool, TerminalWriteTool>();
        services.AddSingleton<IAgentTool, TerminalResizeTool>();
        services.AddSingleton<IAgentTool, TerminalStopTool>();
    }

    private static void RegisterAgentRuntime(IServiceCollection services)
    {
        services.AddSingleton<ToolRegistry>();
        services.AddSingleton<IToolCatalog>(static provider => provider.GetRequiredService<ToolRegistry>());
        services.AddSingleton<IMutableToolCatalog>(static provider => provider.GetRequiredService<ToolRegistry>());
        services.AddSingleton<McpHttpClientFactory>();
        services.AddSingleton<IMcpManager, McpManager>();
        services.AddSingleton<AppStartupViewModel>();
        services.AddSingleton<AppStartupService>();
        services.AddSingleton<ApprovalViewModel>();
        services.AddSingleton<IToolApprovalService>(provider =>
        {
            var approval = provider.GetRequiredService<ApprovalViewModel>();
            return new PolicyApprovalService((descriptor, request, context, cancellationToken) =>
                approval.RequestAsync(descriptor, request, context, cancellationToken));
        });
        services.AddSingleton<IToolExecutor, ToolExecutor>();
        services.AddSingleton<IAgentRunner, AgentRunner>();
    }

    private static void RegisterWorkspace(IServiceCollection services)
    {
        services.AddSingleton<ProxySettingsViewModel>();
        services.AddSingleton<SourceAccountSettingsViewModel>();
        services.AddSingleton<PullRequestsViewModel>();
        services.AddSingleton<ProviderSettingsViewModel>();
        services.AddSingleton<ModelSettingsViewModel>();
        services.AddSingleton<SubagentSettingsViewModel>();
        services.AddSingleton<AgentSettingsViewModel>();
        services.AddSingleton<WorkspaceTaskFactory>();
        services.AddSingleton<WorkspaceProjectService>();
        services.AddSingleton<WorkspaceAttachmentManagerViewModel>();
        services.AddSingleton<ConversationAttachmentThumbnailFactory>();
        services.AddSingleton<WorkspaceRunTracker>();
        services.AddSingleton<WorkspaceRunExecutor>();
        services.AddSingleton<WorkspaceRunController>();
        services.AddSingleton<WorkspaceTaskPanelCoordinator>();
        services.AddSingleton<WorkspaceRepositoryStatusViewModel>();
        services.AddSingleton<WorkspaceCatalogViewModel>();
        services.AddSingleton<WorkspaceFileChangeUndoService>();
        services.AddSingleton<WorkspaceFileChangeStatisticsService>();
        services.AddSingleton<TerminalPaneViewModel>();
        services.AddSingleton<DiffPaneViewModel>();
        services.AddSingleton<FilePreviewPaneViewModel>();
        services.AddSingleton<AuditPaneViewModel>();
        services.AddSingleton<BrowserSettingsViewModel>();
        services.AddSingleton<McpSettingsViewModel>();
        services.AddSingleton<WorkspaceViewModel>();
        services.AddSingleton<IAutomationTaskRunner, WorkspaceAutomationRunner>();
        services.AddSingleton<IAutomationAuthorizationGate, AutomationAuthorizationGate>();
        services.AddSingleton<AutomationScheduler>();
        services.AddSingleton<AutomationViewModel>();
        services.AddSingleton<IAppearancePreferenceStore, JsonAppearancePreferenceStore>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<ICodeFontService, CodeFontService>();
        services.AddSingleton<IUiFontService, UiFontService>();
        services.AddSingleton<IContentFontService, ContentFontService>();
        services.AddSingleton<ThemePaletteSettingsViewModel>();
        services.AddSingleton<IFolderPickerService, AvaloniaFolderPickerService>();
        services.AddSingleton<IFilePickerService, AvaloniaFilePickerService>();
        services.AddSingleton<MainWindowViewModel>();
    }
}

