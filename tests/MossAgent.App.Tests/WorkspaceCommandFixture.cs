using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

internal sealed class WorkspaceCommandFixture : IAsyncDisposable
{
    private readonly CancellationTokenSource _timeout = CancellationTokenSource.CreateLinkedTokenSource(
        TestContext.Current.CancellationToken);
    private readonly bool _keepForReview;
    private ServiceProvider? _services;
    private WorkspaceCommandModelServer? _server;
    private WorkspaceViewModel? _workspace;

    private WorkspaceCommandFixture(string? reviewRoot)
    {
        Root = reviewRoot is null
            ? Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"))
            : Path.GetFullPath(reviewRoot);
        if (Directory.Exists(Root)) throw new InvalidOperationException("Command fixture root must be new.");
        _keepForReview = reviewRoot is not null;
        ProjectRoot = Path.Combine(Root, "project");
        Directory.CreateDirectory(ProjectRoot);
        _timeout.CancelAfter(TimeSpan.FromSeconds(30));
    }

    public string Root { get; }
    public string ProjectRoot { get; }
    public string MarkerPath => Path.Combine(ProjectRoot, "completed.txt");
    public CancellationToken Token => _timeout.Token;
    public WorkspaceViewModel Workspace => _workspace!;
    public ApprovalViewModel Approval => _services!.GetRequiredService<ApprovalViewModel>();
    public WorkspaceCommandModelServer Server => _server!;
    public Guid TaskId => Workspace.Catalog.SelectedTask!.Id;

    public static async Task<WorkspaceCommandFixture> CreateAsync(
        string command, bool escape = false, string? reviewRoot = null)
    {
        var fixture = new WorkspaceCommandFixture(reviewRoot);
        try
        {
            var outside = Path.Combine(fixture.Root, "outside");
            Directory.CreateDirectory(outside);
            fixture._server = new WorkspaceCommandModelServer(command,
                escape ? outside : fixture.ProjectRoot, fixture.Token);
            fixture._services = AppComposition.CreateServices(Path.Combine(fixture.Root, "data"));
            await fixture._services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
            await fixture.ConfigureModelAsync();
            await fixture._services.GetRequiredService<WorkspaceProjectService>()
                .CreateAsync("Command Flow", [fixture.ProjectRoot]);
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
        providers.Name = "command-flow-fixture";
        providers.Protocol = ProviderProtocol.OpenAiResponses;
        providers.BaseUrl = Server.BaseUri.AbsoluteUri;
        providers.ApiKey = "synthetic-fixture-key";
        providers.IsDefault = true;
        await providers.SaveCommand.ExecuteAsync(null);
        Assert.False(providers.HasValidationError, providers.Status);
        var models = services.GetRequiredService<ModelSettingsViewModel>();
        await models.RefreshProvidersCommand.ExecuteAsync(null);
        models.NewModelCommand.Execute(null);
        models.ModelId = "command-flow-model";
        models.DisplayName = "Command Flow Model";
        models.ContextLength = 128000;
        models.MaximumOutputTokens = 1024;
        models.ReasoningEffort = "off";
        models.IsDefault = true;
        await models.SaveModelCommand.ExecuteAsync(null);
        Assert.False(models.HasStatusError, models.Status);
    }

    public void Start(ApprovalPolicy policy = ApprovalPolicy.AskEveryTime)
    {
        Workspace.SetApprovalPolicyCommand.Execute(policy);
        Workspace.ComposerText = "请运行本机验收模型提出的测试命令，仅操作验收项目。";
        Assert.True(Workspace.PrimaryActionCommand.CanExecute(null));
        Workspace.PrimaryActionCommand.Execute(null);
    }

    public Task WaitForApprovalAsync() => WaitUntilAsync(() => Approval.IsPending, Token);

    public async Task<ChatMessageViewModel> WaitForToolPhaseAsync(ToolExecutionPhase phase)
    {
        await WaitUntilAsync(() => Workspace.Messages.Any(message => message.IsToolRunning && message.ToolPhase == phase), Token);
        return Assert.Single(Workspace.Messages, message => message.IsToolRunning && message.ToolPhase == phase);
    }

    public static string PublishPid(string name, string value) =>
        $"[IO.File]::WriteAllText('{name}.tmp', [string]{value}); [IO.File]::Move('{name}.tmp', '{name}')";

    public Task WaitForFileAsync(string name) => WaitUntilAsync(
        () => File.Exists(Path.Combine(ProjectRoot, name)), Token);

    public async Task WaitForCompletionAsync()
    {
        await WaitUntilAsync(() => Workspace.Catalog.SelectedTask is not null && !Workspace.IsRunning, Token);
        if (Workspace.Catalog.SelectedTask!.Status != AgentTaskStatus.Cancelled)
            await Server.Completion.WaitAsync(Token);
    }

    public async Task<ToolResult> AssertPersistedAsync(
        string? error, bool? approvalGranted, AgentTaskStatus status = AgentTaskStatus.Completed)
    {
        await using var reopened = AppComposition.CreateServices(Path.Combine(Root, "data"));
        var repository = reopened.GetRequiredService<IWorkspaceRepository>();
        var savedTask = Assert.Single(await repository.GetTasksAsync(Workspace.Catalog.SelectedProject!.Id, Token));
        Assert.Equal(status, savedTask.Status);
        var history = await repository.GetMessagesAsync(TaskId, Token);
        var toolMessage = Assert.Single(history, message => message.ToolCallId == "command-flow");
        var reloaded = WorkspaceConversationMapper.ToChatMessage(toolMessage);
        var result = new ToolResult(reloaded.Tool!.IsSuccess, reloaded.Tool.Summary,
            reloaded.Tool.Details, ErrorCode: reloaded.Tool.ErrorCode, Dialogue: reloaded.Tool.Dialogue);
        Assert.False(reloaded.IsToolRunning);
        Assert.Null(reloaded.ToolPhase);
        Assert.Equal(error, reloaded.Tool!.ErrorCode);
        Assert.Equal(error is null, reloaded.Tool.IsSuccess);
        var audit = Assert.Single(await reopened.GetRequiredService<IToolExecutionAuditReader>()
            .GetRecentAsync(TaskId, 10, Token));
        Assert.Equal("shell.run", audit.ToolName);
        Assert.Equal(approvalGranted, audit.ApprovalGranted);
        Assert.Equal(error, audit.ErrorCode);
        Assert.Equal(error is null, audit.IsSuccess);
        Assert.NotNull(audit.CompletedAt);
        Assert.Equal(result.OutputCharacterCount, audit.OutputLength);
        var visible = Assert.Single(Workspace.Messages, message => message.ToolCallId == "command-flow");
        Assert.Equal(reloaded.Tool.Summary, visible.Tool!.Summary);
        Assert.Equal(reloaded.Tool.Details, visible.Tool.Details);
        Assert.Equal(reloaded.Tool.ErrorCode, visible.Tool.ErrorCode);
        Assert.Equal(reloaded.Tool.IsSuccess, visible.Tool.IsSuccess);
        Assert.DoesNotContain(Workspace.Messages, message => message.IsToolRunning);
        Assert.False(Approval.IsPending);
        Assert.Equal(0, Approval.PendingCount);
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
        if (_server is not null) await _server.DisposeAsync();
        if (_services is not null) await _services.DisposeAsync();
        _timeout.Dispose();
        SqliteTestPool.Clear(Path.Combine(Root, "data"));
        if (!_keepForReview) await DeleteFixtureAsync();
    }

    private async Task DeleteFixtureAsync()
    {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (true)
        {
            try { Directory.Delete(Root, recursive: true); return; }
            catch (IOException) when (!cleanup.IsCancellationRequested)
            {
                await Task.Delay(30, cleanup.Token);
            }
        }
    }
}
