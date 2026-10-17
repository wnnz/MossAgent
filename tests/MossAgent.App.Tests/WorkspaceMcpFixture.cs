using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Mcp;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

internal sealed class WorkspaceMcpFixture : IAsyncDisposable
{
    private readonly CancellationTokenSource _timeout = CancellationTokenSource.CreateLinkedTokenSource(
        TestContext.Current.CancellationToken);
    private readonly bool _keepForReview;
    private ServiceProvider? _services;
    private WorkspaceToolCallModelServer? _model;
    private WorkspaceViewModel? _workspace;
    private WorkspaceMcpHttpServer? _httpServer;

    private WorkspaceMcpFixture(string? reviewRoot)
    {
        Root = reviewRoot is null
            ? Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"))
            : Path.GetFullPath(reviewRoot);
        if (Directory.Exists(Root)) throw new InvalidOperationException("MCP fixture root must be new.");
        _keepForReview = reviewRoot is not null;
        ProjectRoot = Path.Combine(Root, "project");
        ServerRoot = Path.Combine(ProjectRoot, "mcp-fixture");
        Directory.CreateDirectory(ServerRoot);
        _timeout.CancelAfter(TimeSpan.FromSeconds(30));
    }

    public string Root { get; }
    public string ProjectRoot { get; }
    public string ServerRoot { get; }
    public CancellationToken Token => _timeout.Token;
    public WorkspaceViewModel Workspace => _workspace!;
    public WorkspaceToolCallModelServer Model => _model!;
    public WorkspaceMcpHttpServer HttpServer => _httpServer!;
    public McpSettingsViewModel McpSettings => _services!.GetRequiredService<McpSettingsViewModel>();
    public IMcpConfigurationRepository McpConfigurations => _services!.GetRequiredService<IMcpConfigurationRepository>();
    public IMcpManager McpManager => _services!.GetRequiredService<IMcpManager>();
    public IToolCatalog Tools => _services!.GetRequiredService<IToolCatalog>();
    public ApprovalViewModel Approval => _services!.GetRequiredService<ApprovalViewModel>();
    public Guid TaskId => Workspace.Catalog.SelectedTask!.Id;
    public string ToolName { get; private set; } = string.Empty;

    public static async Task<WorkspaceMcpFixture> CreateAsync(string message, string? reviewRoot = null,
        McpTransportKind transport = McpTransportKind.Stdio, JsonElement? arguments = null, JsonElement? schema = null)
    {
        var fixture = new WorkspaceMcpFixture(reviewRoot);
        try
        {
            if (transport == McpTransportKind.StreamableHttp) fixture._httpServer = new WorkspaceMcpHttpServer(schema);
            fixture._services = AppComposition.CreateServices(Path.Combine(fixture.Root, "data"));
            await fixture._services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
            await fixture.ConfigureMcpAsync();
            var profile = Assert.Single(fixture.McpSettings.Items);
            fixture._model = new WorkspaceToolCallModelServer($"mcp.workflow.{profile.Id:N}",
                arguments ?? JsonSerializer.SerializeToElement(new { message }), fixture.Token, useNamespacePrefix: true);
            await fixture.ConfigureModelAsync();
            await fixture._services.GetRequiredService<WorkspaceProjectService>()
                .CreateAsync("MCP Flow", [fixture.ProjectRoot]);
            fixture._workspace = fixture._services.GetRequiredService<WorkspaceViewModel>();
            await fixture.Workspace.LoadAsync();
            Assert.NotNull(fixture.Workspace.SelectedModel);
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    private async Task ConfigureModelAsync()
    {
        var services = _services!;
        var providers = services.GetRequiredService<ProviderSettingsViewModel>();
        await providers.RefreshCommand.ExecuteAsync(null);
        providers.OpenCreateFormCommand.Execute(null);
        providers.Name = "mcp-flow-fixture";
        providers.Protocol = ProviderProtocol.OpenAiResponses;
        providers.BaseUrl = Model.BaseUri.AbsoluteUri;
        providers.ApiKey = "synthetic-fixture-key";
        providers.IsDefault = true;
        await providers.SaveCommand.ExecuteAsync(null);
        Assert.False(providers.HasValidationError, providers.Status);
        var models = services.GetRequiredService<ModelSettingsViewModel>();
        await models.RefreshProvidersCommand.ExecuteAsync(null);
        models.NewModelCommand.Execute(null);
        models.ModelId = "mcp-flow-model";
        models.DisplayName = "MCP Flow Model";
        models.ContextLength = 128000;
        models.MaximumOutputTokens = 1024;
        models.ReasoningEffort = "off";
        models.IsDefault = true;
        await models.SaveModelCommand.ExecuteAsync(null);
        Assert.False(models.HasStatusError, models.Status);
    }

    private async Task ConfigureMcpAsync()
    {
        var services = _services!;
        var settings = services.GetRequiredService<McpSettingsViewModel>();
        await settings.ReloadCommand.ExecuteAsync(null);
        settings.OpenCreateFormCommand.Execute(null);
        settings.Name = "workflow";
        if (_httpServer is not null)
        {
            settings.Transport = McpTransportKind.StreamableHttp;
            settings.Url = _httpServer.Endpoint.AbsoluteUri;
            settings.HeadersJson = "{\"X-Moss-Fixture\":\"workflow-header\"}";
        }
        else ConfigureStdio(settings);
        settings.IsEnabled = true;
        await settings.SaveCommand.ExecuteAsync(null);
        Assert.False(settings.IsFormOpen, settings.Status);
        Assert.Empty(services.GetRequiredService<IMcpManager>().Errors);
        var descriptor = Assert.Single(services.GetRequiredService<IToolCatalog>().Descriptors,
            tool => tool.Name.StartsWith("mcp.workflow.", StringComparison.Ordinal));
        ToolName = descriptor.Name;
        Assert.Equal(ToolRiskLevel.ExternalSideEffect, descriptor.RiskLevel);
    }

    private void ConfigureStdio(McpSettingsViewModel settings)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var script = Path.Combine(directory.FullName, "tests", "fixtures", "mcp-lifecycle-server.ps1");
        settings.Transport = McpTransportKind.Stdio;
        settings.Command = "pwsh";
        settings.ArgumentsJson = JsonSerializer.Serialize(new[]
            { "-NoProfile", "-File", script, "-FixtureArgument", "界面参数 with spaces" });
        settings.WorkingDirectory = ServerRoot;
        settings.EnvironmentJson = JsonSerializer.Serialize(new
        {
            MOSSAGENT_MCP_FIXTURE_MODE = "echo",
            MOSSAGENT_MCP_FIXTURE_CHILD = "0",
            MOSSAGENT_MCP_FIXTURE_VALUE = "界面环境 🚀"
        });
    }

    public void Start(ApprovalPolicy policy = ApprovalPolicy.AskEveryTime)
    {
        Workspace.SetApprovalPolicyCommand.Execute(policy);
        Workspace.ComposerText = "调用本机 MCP 验收服务，仅操作验收项目。";
        Assert.True(Workspace.PrimaryActionCommand.CanExecute(null));
        Workspace.PrimaryActionCommand.Execute(null);
    }

    public Task WaitForApprovalAsync() => WaitUntilAsync(() => Approval.IsPending, Token);

    public Task WaitForFileAsync(string name) => WaitUntilAsync(
        () => File.Exists(Path.Combine(ServerRoot, name)), Token);

    public async Task WaitForCompletionAsync()
    {
        await WaitUntilAsync(() => Workspace.Catalog.SelectedTask is not null && !Workspace.IsRunning, Token);
        if (Workspace.Catalog.SelectedTask!.Status != AgentTaskStatus.Cancelled)
            await Model.Completion.WaitAsync(Token);
    }

    public async Task ReopenAsync()
    {
        var taskId = TaskId;
        await _services!.DisposeAsync();
        _services = AppComposition.CreateServices(Path.Combine(Root, "data"));
        _workspace = _services.GetRequiredService<WorkspaceViewModel>();
        await Workspace.LoadAsync();
        await WaitUntilAsync(() => Workspace.Catalog.SelectedTask?.Id == taskId && Workspace.Messages.Count > 0, Token);
    }

    public async Task<ToolResult> AssertPersistedAsync(string? error, bool? approvalGranted,
        AgentTaskStatus status = AgentTaskStatus.Completed)
    {
        await using var reopened = AppComposition.CreateServices(Path.Combine(Root, "data"));
        var repository = reopened.GetRequiredService<IWorkspaceRepository>();
        var saved = Assert.Single(await repository.GetTasksAsync(Workspace.Catalog.SelectedProject!.Id, Token));
        Assert.Equal(status, saved.Status);
        var history = await repository.GetMessagesAsync(TaskId, Token);
        var reloaded = WorkspaceConversationMapper.ToChatMessage(Assert.Single(history,
            message => message.ToolCallId == "mcp-flow"));
        var result = new ToolResult(reloaded.Tool!.IsSuccess, reloaded.Tool.Summary,
            reloaded.Tool.Details, ErrorCode: reloaded.Tool.ErrorCode);
        Assert.False(reloaded.IsToolRunning);
        Assert.Equal(error, result.ErrorCode);
        Assert.Equal(error is null, result.IsSuccess);
        var audit = Assert.Single(await reopened.GetRequiredService<IToolExecutionAuditReader>()
            .GetRecentAsync(TaskId, 10, Token));
        Assert.Equal(ToolName, audit.ToolName);
        Assert.Equal(approvalGranted, audit.ApprovalGranted);
        Assert.Equal(error, audit.ErrorCode);
        Assert.Equal(error is null, audit.IsSuccess);
        Assert.NotNull(audit.CompletedAt);
        Assert.Equal(result.OutputCharacterCount, audit.OutputLength);
        var visible = Assert.Single(Workspace.Messages, message => message.ToolCallId == "mcp-flow");
        Assert.Equal(reloaded.Tool.Summary, visible.Tool!.Summary);
        Assert.Equal(reloaded.Tool.Details, visible.Tool.Details);
        Assert.Equal(reloaded.Tool.ErrorCode, visible.Tool.ErrorCode);
        Assert.DoesNotContain(Workspace.Messages, message => message.IsToolRunning);
        Assert.False(Approval.IsPending);
        return result;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken token)
    {
        while (!condition()) await Task.Delay(15, token);
    }

    public async ValueTask DisposeAsync()
    {
        if (_workspace is { IsRunning: true } workspace)
        {
            workspace.PrimaryActionCommand.Execute(null);
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await WaitUntilAsync(() => !workspace.IsRunning, cleanup.Token);
        }
        if (_model is not null) await _model.DisposeAsync();
        if (_services is not null) await _services.DisposeAsync();
        if (_httpServer is not null) await _httpServer.DisposeAsync();
        _timeout.Dispose();
        SqliteTestPool.Clear(Path.Combine(Root, "data"));
        if (_keepForReview) return;
        using var deletion = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            try { Directory.Delete(Root, recursive: true); return; }
            catch (IOException) when (!deletion.IsCancellationRequested)
            {
                await Task.Delay(30, deletion.Token);
            }
        }
    }
}
