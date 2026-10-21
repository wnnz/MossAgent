using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Browser;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class BrowserArtifactAgentFlowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, "success")]
    [InlineData(true, "success")]
    [InlineData(false, "denied")]
    [InlineData(true, "denied")]
    [InlineData(false, "refused")]
    [InlineData(true, "refused")]
    [InlineData(false, "cancelled")]
    [InlineData(true, "cancelled")]
    public Task SavedModelThroughApprovalAndNativeArtifactPersistsVisibleResultAndRetainsDraft(bool download, string scenario) => _session.DispatchAsync(async () =>
    {
        var browser = new BrowserArtifactTestSession();
        var manager = new BrowserArtifactSessionManager(browser);
        var prepared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (scenario == "cancelled")
        {
            browser.Capture = token => AwaitCancellationAsync(prepared, token);
            browser.Transfer = (_, token) => AwaitCancellationAsync(prepared, token);
        }
        var sessions = new ObservedArtifactSessions { Created = (_, _) =>
        {
            if (scenario == "refused") throw new FileMutationSessionException("synthetic safe artifact refusal", "artifact_boundary_refused");
        } };
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(false, "", configure: services =>
        {
            services.Replace(ServiceDescriptor.Singleton<IArtifactFileSessionFactory>(sessions));
            services.Replace(ServiceDescriptor.Singleton<ITaskBrowserSessionManager>(manager));
        }, mutationFactory: (_, _) => download ? new("browser_download__", new { selector = "#download" }) : new("browser_screenshot__", new { fullPage = true }));
        fixture.Start();
        await fixture.WaitForApprovalAsync();
        Assert.Equal(download ? "browser.download" : "browser.screenshot", fixture.Approval.ToolName);
        Assert.Empty(sessions.Sessions);
        Assert.Equal(0, browser.Captures + browser.Downloads);
        if (scenario == "denied") fixture.Approval.DenyCommand.Execute(fixture.Approval.CurrentRequestId);
        else fixture.Approval.ApproveCommand.Execute(fixture.Approval.CurrentRequestId);
        fixture.Workspace.ComposerText = "保留浏览器任务草稿";
        if (scenario == "cancelled")
        {
            await prepared.Task.WaitAsync(fixture.Token);
            fixture.Workspace.PrimaryActionCommand.Execute(null);
        }
        await fixture.WaitForCompletionAsync();
        var error = scenario switch { "denied" => "approval_denied", "refused" => "artifact_boundary_refused", "cancelled" => "tool_cancelled", _ => null };
        Assert.Equal(manager.Owner, fixture.TaskId);
        Assert.All(sessions.Sessions, session => Assert.False(session.Stream.CanRead));
        Assert.Equal("保留浏览器任务草稿", fixture.Workspace.ComposerText);
        Assert.Equal("alpha\n", await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
        Assert.Equal("outside sentinel", await File.ReadAllTextAsync(fixture.OutsidePath, fixture.Token));
        await AssertReloadedAsync(fixture, error, scenario != "denied");
    }, TestContext.Current.CancellationToken);

    private static async Task AwaitCancellationAsync(TaskCompletionSource prepared, CancellationToken token)
    {
        prepared.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, token);
    }

    private static async Task AssertReloadedAsync(WorkspaceFileEditingFixture fixture, string? error, bool approved)
    {
        await using var restored = AppComposition.CreateServices(Path.Combine(fixture.Root, "data"));
        var repository = restored.GetRequiredService<IWorkspaceRepository>();
        var history = await repository.GetMessagesAsync(fixture.TaskId, fixture.Token);
        var row = WorkspaceConversationMapper.ToChatMessage(Assert.Single(history, message => message.ToolCallId == "file-edit"));
        Assert.Equal(error, row.Tool!.ErrorCode);
        Assert.Equal(error is null, row.Tool.IsSuccess);
        Assert.Equal(error, Assert.Single(fixture.Workspace.Messages, message => message.ToolCallId == "file-edit").Tool!.ErrorCode);
        var audits = await restored.GetRequiredService<IToolExecutionAuditReader>().GetRecentAsync(fixture.TaskId, 10, fixture.Token);
        var audit = Assert.Single(audits, item => item.CallId == "file-edit");
        Assert.Equal(approved, audit.ApprovalGranted);
        Assert.Equal(error, audit.ErrorCode);
        Assert.NotNull(audit.CompletedAt);
        var artifacts = await restored.GetRequiredService<IToolExecutionAuditReader>().GetArtifactsAsync(audit.Id, fixture.Token);
        Assert.Equal(error is null ? 1 : 0, artifacts.Count);
        var workspace = restored.GetRequiredService<WorkspaceViewModel>();
        await workspace.LoadAsync();
        Assert.False(workspace.IsRunning);
        Assert.Equal(error == "tool_cancelled" ? AgentTaskStatus.Cancelled : AgentTaskStatus.Completed, workspace.Catalog.SelectedTask!.Status);
        if (error is null)
        {
            await workspace.FilePreview.OpenArtifactAsync(Assert.Single(artifacts), fixture.Token);
            Assert.True(workspace.FilePreview.Active!.IsReady, workspace.FilePreview.Active.Status);
            Assert.Equal(3, fixture.Server.Requests.Count);
        }
        Assert.All(fixture.Server.Requests, request => Assert.Contains("file-flow-model", request));
    }
}
