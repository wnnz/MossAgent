using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

internal sealed class WorkspaceFileEditingFixture : IAsyncDisposable
{
    private readonly CancellationTokenSource _timeout = CancellationTokenSource.CreateLinkedTokenSource(
        TestContext.Current.CancellationToken);
    private ServiceProvider? _services;
    private FileEditingModelServer? _server;
    private WorkspaceViewModel? _workspace;

    private WorkspaceFileEditingFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        ProjectRoot = Path.Combine(Root, "project");
        Directory.CreateDirectory(ProjectRoot);
        FilePath = Path.Combine(ProjectRoot, "fixture.txt");
        OutsidePath = Path.Combine(Root, "outside.txt");
        _timeout.CancelAfter(TimeSpan.FromSeconds(20));
    }

    public string Root { get; }
    public string ProjectRoot { get; }
    public string FilePath { get; }
    public string OutsidePath { get; }
    public CancellationToken Token => _timeout.Token;
    public WorkspaceViewModel Workspace => _workspace!;
    public ServiceProvider Services => _services!;
    public ApprovalViewModel Approval => _services!.GetRequiredService<ApprovalViewModel>();
    public FileEditingModelServer Server => _server!;
    public Guid TaskId => Workspace.Catalog.SelectedTask!.Id;

    public static async Task<WorkspaceFileEditingFixture> CreateAsync(bool replace, string content, bool escape = false)
    {
        var fixture = new WorkspaceFileEditingFixture();
        try
        {
            await File.WriteAllTextAsync(fixture.FilePath, "alpha\n", fixture.Token);
            await File.WriteAllTextAsync(fixture.OutsidePath, "outside sentinel", fixture.Token);
            fixture._server = new FileEditingModelServer(fixture.FilePath,
                escape ? fixture.OutsidePath : fixture.FilePath, replace, content, fixture.Token);
            fixture._services = AppComposition.CreateServices(Path.Combine(fixture.Root, "data"));
            await fixture._services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
            await fixture.ConfigureModelAsync();
            await fixture._services.GetRequiredService<WorkspaceProjectService>().CreateAsync("File Flow", [fixture.ProjectRoot]);
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
        providers.Name = "file-flow-fixture";
        providers.Protocol = ProviderProtocol.OpenAiResponses;
        providers.BaseUrl = Server.BaseUri.AbsoluteUri;
        providers.ApiKey = "synthetic-fixture-key";
        providers.IsDefault = true;
        await providers.SaveCommand.ExecuteAsync(null);
        Assert.False(providers.HasValidationError, providers.Status);
        var models = services.GetRequiredService<ModelSettingsViewModel>();
        await models.RefreshProvidersCommand.ExecuteAsync(null);
        models.NewModelCommand.Execute(null);
        models.ModelId = "file-flow-model";
        models.DisplayName = "File Flow Model";
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
        Workspace.ComposerText = "Read fixture.txt and apply the local model's proposed edit.";
        Assert.True(Workspace.PrimaryActionCommand.CanExecute(null));
        Workspace.PrimaryActionCommand.Execute(null);
    }

    public Task WaitForApprovalAsync() => WaitUntilAsync(() => Approval.IsPending, Token);

    public async Task WaitForCompletionAsync()
    {
        await WaitUntilAsync(() => Workspace.Catalog.SelectedTask is not null && !Workspace.IsRunning, Token);
        if (Workspace.Catalog.SelectedTask!.Status != AgentTaskStatus.Cancelled)
            await Server.Completion.WaitAsync(Token);
    }

    public async Task AssertPersistedAsync(string? error, bool? approvalGranted, AgentTaskStatus status)
    {
        await using var reopened = AppComposition.CreateServices(Path.Combine(Root, "data"));
        var repository = reopened.GetRequiredService<IWorkspaceRepository>();
        var savedTask = Assert.Single(await repository.GetTasksAsync(Workspace.Catalog.SelectedProject!.Id, Token));
        Assert.Equal(status, savedTask.Status);
        var history = await repository.GetMessagesAsync(TaskId, Token);
        var reloaded = WorkspaceConversationMapper.ToChatMessage(Assert.Single(history,
            message => message.ToolCallId == "file-edit"));
        Assert.False(reloaded.IsToolRunning);
        Assert.Equal(error, reloaded.Tool!.ErrorCode);
        Assert.Equal(error is null, reloaded.Tool.IsSuccess);
        var audits = await reopened.GetRequiredService<IToolExecutionAuditReader>().GetRecentAsync(TaskId, 10, Token);
        Assert.True(Assert.Single(audits, audit => audit.CallId == "file-read").IsSuccess);
        var edit = Assert.Single(audits, audit => audit.CallId == "file-edit");
        Assert.Equal(approvalGranted, edit.ApprovalGranted);
        Assert.Equal(error, edit.ErrorCode);
        Assert.Equal(error is null, edit.IsSuccess);
        Assert.NotNull(edit.CompletedAt);
        var visible = Assert.Single(Workspace.Messages, message => message.ToolCallId == "file-edit");
        Assert.Equal(reloaded.Tool.IsSuccess, visible.Tool!.IsSuccess);
        Assert.Equal(reloaded.Tool.ErrorCode, visible.Tool.ErrorCode);
        Assert.Equal(reloaded.Tool.FileChanges, visible.Tool.FileChanges);
        if (error is null)
        {
            var change = Assert.Single(reloaded.Tool.FileChanges!);
            Assert.True(change.IsValid);
            Assert.Equal(FilePath, change.Path);
            Assert.Equal(Server.ExpectedSha256, change.BeforeSha256, ignoreCase: true);
            Assert.NotNull(change.BeforeSnapshot);
            Assert.NotNull(change.AfterSnapshot);
            Assert.Equal(change.BeforeSha256, change.BeforeSnapshot.Sha256, ignoreCase: true);
            Assert.Equal(change.AfterSha256, change.AfterSnapshot.Sha256, ignoreCase: true);
            Assert.Equal(FilePath, Assert.Single(Workspace.FileChanges.Files).Path);
        }
        else
        {
            Assert.Null(reloaded.Tool.FileChanges);
            Assert.False(Workspace.FileChanges.HasChanges);
        }
        Assert.DoesNotContain(Workspace.Messages, message => message.IsToolRunning);
        Assert.False(Approval.IsPending);
        Assert.Equal(0, Approval.PendingCount);
        Assert.DoesNotContain(Directory.EnumerateFiles(ProjectRoot), path => path != FilePath);
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
        await DeleteFixtureAsync();
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
