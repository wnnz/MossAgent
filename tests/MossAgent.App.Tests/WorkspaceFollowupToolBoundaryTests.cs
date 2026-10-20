using System.Text.Json;
using Avalonia.Headless;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceFollowupToolBoundaryTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(ApprovalPolicy.AskEveryTime, true)]
    [InlineData(ApprovalPolicy.AskEveryTime, false)]
    [InlineData(ApprovalPolicy.ReadOnly, false)]
    public Task QueuedWritesUseTheirSavedPolicyAndPublishRealResultsAndAudits(ApprovalPolicy policy, bool approve) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true);
        var workspace = fixture.Workspace;
        var path = Path.Combine(workspace.Catalog.SelectedProject!.PrimaryDirectory, "queued-write.txt");
        fixture.Server.ResponseOverride = (request, number) => number == 2 ? WriteCall(request, path) : null;
        workspace.SetApprovalPolicyCommand.Execute(policy);
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        var taskId = workspace.Catalog.SelectedTask!.Id;
        foreach (var prompt in new[] { "排队的文件写入", "审批之后的下一条" })
        {
            workspace.ComposerText = prompt;
            await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
        }
        workspace.ComposerText = "审批期间保留的草稿";
        workspace.SetApprovalPolicyCommand.Execute(ApprovalPolicy.FullAccess);
        fixture.Server.ReleaseResponses();
        if (policy == ApprovalPolicy.AskEveryTime)
        {
            await WaitForWriteApprovalAsync(fixture);
            Assert.Equal(2, fixture.Server.Requests.Count);
            Assert.True(workspace.HasActiveRun);
            Assert.False(File.Exists(path));
            Assert.Single((await fixture.Followups.GetAsync(taskId, fixture.Token)).Items);
            var approval = fixture.Main.Approval;
            (approve ? approval.ApproveCommand : approval.DenyCommand).Execute(approval.CurrentRequestId);
        }
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 4 && !workspace.IsRunning && !workspace.Queue.IsBusy);
        Assert.Equal(approve, File.Exists(path));
        if (approve) Assert.Equal("queued-content", await File.ReadAllTextAsync(path, fixture.Token));
        var audit = await WorkspaceFollowupAuditAssertions.ReadWriteAsync(fixture, taskId, approve);
        Assert.Equal("queued-write", audit.CallId);
        Assert.Equal("filesystem.write_file", audit.ToolName);
        Assert.Equal(approve, audit.IsSuccess);
        Assert.Equal(approve ? null : policy == ApprovalPolicy.ReadOnly ? "capability_denied" : "approval_denied", audit.ErrorCode);
        Assert.Equal(policy, audit.ApprovalPolicy);
        Assert.Equal(approve, audit.ApprovalGranted);
        Assert.NotNull(audit.CompletedAt);
        var visible = Assert.Single(workspace.Messages, message => message.ToolCallId == "queued-write");
        Assert.Equal(audit.ErrorCode, visible.Tool!.ErrorCode);
        Assert.Equal(audit.IsSuccess, visible.Tool.IsSuccess);
        var history = await fixture.Tasks.GetMessagesAsync(taskId, fixture.Token);
        var restored = MossAgent.App.ViewModels.WorkspaceConversationMapper.ToChatMessage(
            Assert.Single(history, message => message.ToolCallId == "queued-write"));
        Assert.Equal(audit.ErrorCode, restored.Tool!.ErrorCode);
        Assert.Equal(audit.IsSuccess, restored.Tool.IsSuccess);
        Assert.Equal("审批期间保留的草稿", workspace.ComposerText);
        Assert.Equal(policy, workspace.Catalog.SelectedTask?.ApprovalPolicy);
        Assert.Empty(workspace.Queue.Items);
        Assert.False(fixture.Main.Approval.IsPending);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CancellingAQueuedToolApprovalKeepsLaterEntriesPausedUntilManualContinuation() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true);
        var workspace = fixture.Workspace;
        var path = Path.Combine(workspace.Catalog.SelectedProject!.PrimaryDirectory, "cancelled-queued-write.txt");
        fixture.Server.ResponseOverride = (request, number) => number == 2 ? WriteCall(request, path) : null;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        foreach (var prompt in new[] { "等待审批的排队写入", "取消后需要手动继续" })
        {
            workspace.ComposerText = prompt;
            await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
        }
        fixture.Server.ReleaseResponses();
        await WaitForWriteApprovalAsync(fixture);
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => !workspace.IsRunning && workspace.Queue.HasNotice && workspace.Queue.Items.Count == 1);
        Assert.Equal(AgentTaskStatus.Cancelled, workspace.Catalog.SelectedTask?.Status);
        Assert.Equal(2, fixture.Server.Requests.Count);
        Assert.False(File.Exists(path));
        Assert.Single(workspace.Queue.Items);
        Assert.True(workspace.Queue.HasNotice);
        Assert.False(fixture.Main.Approval.IsPending);
        var taskId = workspace.Catalog.SelectedTask!.Id;
        Assert.Equal("tool_cancelled", Assert.Single(await fixture.ToolAudits.GetRecentAsync(taskId, 10, fixture.Token)).ErrorCode);
        await workspace.Queue.ContinueCommand.ExecuteAsync(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 3 && !workspace.IsRunning);
        Assert.False(File.Exists(path));
        Assert.Empty(workspace.Queue.Items);
        Assert.Single(await fixture.ToolAudits.GetRecentAsync(taskId, 10, fixture.Token));
        Assert.Equal(AgentTaskStatus.Completed, workspace.Catalog.SelectedTask?.Status);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task AnUnterminatedToolCallDoesNotExecuteOrAdvanceTheQueue() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true);
        var workspace = fixture.Workspace;
        var path = Path.Combine(workspace.Catalog.SelectedProject!.PrimaryDirectory, "incomplete-write.txt");
        fixture.Server.ResponseOverride = (request, number) => number == 1 ? WriteCall(request, path) : null;
        fixture.Server.InterruptNextResponse(empty: false);
        workspace.SetApprovalPolicyCommand.Execute(ApprovalPolicy.FullAccess);
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        workspace.ComposerText = "未完整调用之后需手动继续";
        await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
        fixture.Server.ReleaseResponses();
        await fixture.WaitUntilAsync(() => !workspace.IsRunning && (workspace.Catalog.SelectedTask?.Status != AgentTaskStatus.Failed
            || workspace.Queue.HasNotice && workspace.Queue.Items.Count == 1));
        Assert.False(File.Exists(path));
        Assert.Equal(AgentTaskStatus.Failed, workspace.Catalog.SelectedTask?.Status);
        Assert.Single(fixture.Server.Requests);
        Assert.False(fixture.Main.Approval.IsPending);
        Assert.Single(workspace.Queue.Items);
        var taskId = workspace.Catalog.SelectedTask!.Id;
        Assert.Empty(await fixture.ToolAudits.GetRecentAsync(taskId, 10, fixture.Token));
        await workspace.Queue.ContinueCommand.ExecuteAsync(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 2 && !workspace.IsRunning);
        Assert.False(File.Exists(path));
        Assert.Empty(await fixture.ToolAudits.GetRecentAsync(taskId, 10, fixture.Token));
        Assert.Empty(workspace.Queue.Items);
        Assert.Equal(AgentTaskStatus.Completed, workspace.Catalog.SelectedTask?.Status);
    }, TestContext.Current.CancellationToken);

    private static Task WaitForWriteApprovalAsync(WorkspaceModelSelectionFixture fixture) =>
        fixture.WaitUntilAsync(() => fixture.Main.Approval is { IsPending: true, ToolName: "filesystem.write_file", CurrentRequestId: { } id } approval
            && approval.ApproveCommand.CanExecute(id) && approval.DenyCommand.CanExecute(id));

    private static string WriteCall(JsonElement request, string path)
    {
        var name = request.GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("name").GetString())
            .Single(value => value?.StartsWith("filesystem_write_file__", StringComparison.Ordinal) == true);
        return JsonSerializer.Serialize(new
        {
            type = "response.output_item.done",
            item = new { type = "function_call", call_id = "queued-write", name,
                arguments = JsonSerializer.Serialize(new { path, content = "queued-content" }) }
        });
    }
}
