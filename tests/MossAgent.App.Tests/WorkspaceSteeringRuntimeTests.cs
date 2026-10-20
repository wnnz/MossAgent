using System.Text.Json;
using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Conversations;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceSteeringRuntimeTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task ASteeredModelToolStillCannotWriteOutsideTheTasksAuthorizedRoots() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await CreateAsync(true);
        var outsidePath = Path.Combine(Path.GetTempPath(), $"moss-steering-outside-{Guid.NewGuid():N}.txt");
        try
        {
            var workspace = fixture.Workspace;
            workspace.SetApprovalPolicyCommand.Execute(ApprovalPolicy.FullAccess);
            fixture.Server.ResponseOverride = (request, number) => number == 2 ? WriteCall(request, outsidePath) : null;
            workspace.PrimaryActionCommand.Execute(null);
            await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
            var taskId = workspace.Catalog.SelectedTask!.Id;
            Assert.True(fixture.Runs.TryGet(taskId, out var state));
            var input = Assert.IsType<ConversationSteeringSession>(state!.Steering);
            workspace.ComposerText = "new guidance cannot grant file permissions";
            await workspace.Steering.SubmitCommand.ExecuteAsync(null);
            Assert.Single(workspace.Steering.History.Items);
            fixture.Server.ReleaseResponses();
            await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 3 && !workspace.IsRunning);
            Assert.False(File.Exists(outsidePath));
            var audit = Assert.Single(await fixture.ToolAudits.GetRecentAsync(taskId, 50, fixture.Token), item => item.CallId == "steered-write");
            Assert.False(audit.IsSuccess);
            Assert.Equal("path_denied", audit.ErrorCode);
            Assert.NotNull(audit.CompletedAt);
            Assert.False(fixture.Main.Approval.IsPending);
            var output = Assert.Single(fixture.Server.Requests[2].GetProperty("input").EnumerateArray(),
                item => item.TryGetProperty("type", out var type) && type.GetString() == "function_call_output"
                    && item.GetProperty("call_id").GetString() == "steered-write");
            using var result = JsonDocument.Parse(output.GetProperty("output").GetString()!);
            Assert.False(result.RootElement.GetProperty("IsSuccess").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(result.RootElement.GetProperty("Summary").GetString()));
            Assert.Empty(workspace.Queue.Items);
        }
        finally { if (File.Exists(outsidePath)) File.Delete(outsidePath); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CancellingOneLiveMailboxPreservesTheOtherRunItsInputAndForegroundDraft() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await CreateAsync(false);
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        var taskA = workspace.Catalog.SelectedTask!.Id;
        Assert.True(fixture.Runs.TryGet(taskA, out var stateA));
        var inputA = Assert.IsType<ConversationSteeringSession>(stateA!.Steering);
        await inputA.AcceptAsync(inputA.RunId, Guid.NewGuid(), "lane A guidance", null, fixture.Token);
        workspace.ComposerText = "lane A draft";
        workspace.Catalog.NewChatCommand.Execute(null);
        workspace.ComposerText = "lane B initial";
        await fixture.WaitUntilAsync(() => workspace.PrimaryActionCommand.CanExecute(null));
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => workspace.Catalog.SelectedTask is { } task && task.Id != taskA
            && fixture.Runs.TryGet(task.Id, out var candidate) && candidate?.Steering is not null);
        var taskB = workspace.Catalog.SelectedTask!.Id;
        Assert.True(fixture.Runs.TryGet(taskB, out var stateB));
        var inputB = Assert.IsType<ConversationSteeringSession>(stateB!.Steering);
        Assert.NotEqual(inputA.RunId, inputB.RunId);
        await inputB.AcceptAsync(inputB.RunId, Guid.NewGuid(), "lane B guidance", null, fixture.Token);
        workspace.ComposerText = "lane B foreground draft";
        fixture.Runs.Cancel(taskA);
        await fixture.WaitUntilAsync(() => !fixture.Runs.IsRunning(taskA));
        Assert.True(fixture.Runs.IsRunning(taskB));
        Assert.True(inputB.IsAccepting);
        Assert.False(inputA.IsAccepting);
        Assert.Equal("lane B foreground draft", workspace.ComposerText);
        fixture.Server.ReleaseResponses();
        await fixture.WaitUntilAsync(() => !fixture.Runs.IsRunning(taskB));
        Assert.Equal(3, fixture.Server.Requests.Count);
        Assert.DoesNotContain("lane A guidance", string.Join("\n", fixture.Server.Requests.Select(Readable)));
        Assert.Contains("lane B guidance", Readable(fixture.Server.Requests[^1]));
        Assert.Equal(ConversationSteeringInputState.Interrupted,
            Assert.Single((await fixture.Steering.GetInputsAsync(taskA, long.MaxValue, 64, fixture.Token)).Items).State);
        Assert.Equal(ConversationSteeringInputState.AppliedAtBoundary,
            Assert.Single((await fixture.Steering.GetInputsAsync(taskB, long.MaxValue, 64, fixture.Token)).Items).State);
        await workspace.Drafts.FlushAsync();
        Assert.Equal("lane A draft", Assert.IsType<ConversationDraft>(await fixture.Drafts.GetAsync(new(null, taskA), fixture.Token)).Text);
        Assert.Equal("lane B foreground draft", Assert.IsType<ConversationDraft>(await fixture.Drafts.GetAsync(new(null, taskB), fixture.Token)).Text);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task FailedStreamKeepsAnInterruptedReceiptAcrossWorkspaceReconstructionWithoutReplay() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await CreateAsync(false);
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        var taskId = workspace.Catalog.SelectedTask!.Id;
        Assert.True(fixture.Runs.TryGet(taskId, out var state));
        var input = Assert.IsType<ConversationSteeringSession>(state!.Steering);
        workspace.ComposerText = "failed stream guidance";
        await workspace.Steering.SubmitCommand.ExecuteAsync(null);
        Assert.Single(workspace.Steering.History.Items);
        workspace.ComposerText = "failure recovery draft";
        fixture.Server.FailNextResponse();
        fixture.Server.ReleaseResponses();
        await fixture.WaitUntilAsync(() => !workspace.IsRunning);
        Assert.Equal(AgentTaskStatus.Failed, workspace.Catalog.SelectedTask?.Status);
        var receipt = Assert.Single((await fixture.Steering.GetInputsAsync(taskId, long.MaxValue, 64, fixture.Token)).Items);
        Assert.Equal(ConversationSteeringInputState.Interrupted, receipt.State);
        Assert.DoesNotContain(await fixture.Tasks.GetMessagesAsync(taskId, fixture.Token), message => message.Id == receipt.Id);
        await workspace.ShutdownAsync();
        using var runs = new WorkspaceRunTracker();
        await using var restored = fixture.CreateWorkspace(fixture.Tasks, runs, conversationDrafts: fixture.Drafts);
        await restored.LoadAsync();
        Assert.Equal("failure recovery draft", restored.ComposerText);
        Assert.Equal(receipt, Assert.Single((await fixture.Steering.GetInputsAsync(taskId, long.MaxValue, 64, fixture.Token)).Items));
        Assert.Single(fixture.Server.Requests);
        Assert.False(restored.HasActiveRun);
        Assert.Empty(restored.Queue.Items);
        await runs.StopAsync();
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task HeldResponseAcceptsGuidanceWithinTheSameRunAndRetainsItsSelectedModelAndDraft() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await CreateAsync(false);
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        var taskId = workspace.Catalog.SelectedTask!.Id;
        Assert.True(fixture.Runs.TryGet(taskId, out var state));
        var input = Assert.IsType<ConversationSteeringSession>(state!.Steering);
        workspace.ComposerText = "当前运行追加指令";
        await workspace.Steering.SubmitCommand.ExecuteAsync(null);
        var ackId = Assert.Single(workspace.Steering.History.Items).Id;
        Assert.True(workspace.ModelPicker.TrySelect(fixture.AlternativeModel.ProviderId, fixture.AlternativeModel.Id));
        workspace.ModelPicker.SelectedReasoning = "高";
        workspace.ComposerText = "仍在编辑的草稿";
        fixture.Server.ReleaseResponses();
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 2 && !workspace.IsRunning);
        Assert.False(input.IsAccepting);
        Assert.Equal("仍在编辑的草稿", workspace.ComposerText);
        Assert.Empty(workspace.Queue.Items);
        Assert.All(fixture.Server.Requests, request =>
        {
            Assert.Equal(fixture.SelectedModel.ModelId, request.GetProperty("model").GetString());
            Assert.Equal("medium", request.GetProperty("reasoning").GetProperty("effort").GetString());
            Assert.False(request.TryGetProperty("tools", out var tools) && tools.GetArrayLength() > 0);
        });
        Assert.DoesNotContain("当前运行追加指令", Readable(fixture.Server.Requests[0]));
        Assert.Contains("当前运行追加指令", Readable(fixture.Server.Requests[1]));
        var task = Assert.Single(await fixture.Tasks.GetTasksAsync(null, fixture.Token));
        Assert.Equal(taskId, task.Id);
        Assert.Equal(AgentTaskStatus.Completed, task.Status);
        Assert.Equal(ApprovalPolicy.ReadOnly, task.ApprovalPolicy);
        var history = await fixture.Tasks.GetMessagesAsync(taskId, fixture.Token);
        Assert.Equal(2, history.Count(message => message.Role == MessageRole.User));
        Assert.Equal(2, history.Count(message => message.Role == MessageRole.Assistant));
        Assert.Single(history, message => message.Id == ackId && message.Content == "当前运行追加指令");
        Assert.Single(workspace.Messages, message => message.Content == "当前运行追加指令");
        var saved = Assert.Single((await fixture.Steering.GetInputsAsync(taskId, long.MaxValue, 64, fixture.Token)).Items);
        Assert.Equal(input.RunId, saved.RunId);
        Assert.Equal(ConversationSteeringInputState.AppliedAtBoundary, saved.State);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("approve")]
    [InlineData("deny")]
    [InlineData("cancel")]
    public Task GuidanceDuringARealWriteApprovalNeverReplaysTheWriteAndCancellationRetainsItsReceipt(string outcome) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await CreateAsync(true);
        var workspace = fixture.Workspace;
        var path = Path.Combine(workspace.Catalog.SelectedProject!.PrimaryDirectory, "steered-write.txt");
        fixture.Server.ResponseOverride = (request, number) => number == 1 ? WriteCall(request, path) : null;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        var taskId = workspace.Catalog.SelectedTask!.Id;
        Assert.True(fixture.Runs.TryGet(taskId, out var state));
        var input = Assert.IsType<ConversationSteeringSession>(state!.Steering);
        fixture.Server.ReleaseResponses();
        var approval = fixture.Main.Approval;
        await fixture.WaitUntilAsync(() => approval.IsPending && approval.ToolName == "filesystem.write_file"
            && approval.CurrentRequestId is { } id && approval.ApproveCommand.CanExecute(id) && approval.DenyCommand.CanExecute(id));
        Assert.False(File.Exists(path));
        workspace.ComposerText = "等待这个工具结果后继续";
        await workspace.Steering.SubmitCommand.ExecuteAsync(null);
        var ackId = Assert.Single(workspace.Steering.History.Items).Id;
        workspace.ComposerText = "审批期间保留的草稿";
        workspace.SetApprovalPolicyCommand.Execute(ApprovalPolicy.FullAccess);
        if (outcome == "cancel") workspace.PrimaryActionCommand.Execute(null);
        else (outcome == "approve" ? approval.ApproveCommand : approval.DenyCommand).Execute(approval.CurrentRequestId);
        await fixture.WaitUntilAsync(() => !workspace.IsRunning);
        Assert.Equal(outcome == "approve", File.Exists(path));
        if (outcome == "approve") Assert.Equal("steered-content", await File.ReadAllTextAsync(path, fixture.Token));
        var audit = Assert.Single(await fixture.ToolAudits.GetRecentAsync(taskId, 50, fixture.Token), item => item.CallId == "steered-write");
        Assert.Equal(outcome == "approve", audit.IsSuccess);
        Assert.Equal(ApprovalPolicy.AskEveryTime, audit.ApprovalPolicy);
        Assert.NotNull(audit.CompletedAt);
        Assert.Equal(outcome == "cancel" ? 1 : 2, fixture.Server.Requests.Count);
        var receipt = Assert.Single((await fixture.Steering.GetInputsAsync(taskId, long.MaxValue, 64, fixture.Token)).Items);
        Assert.Equal(ackId, receipt.Id);
        Assert.Equal(outcome == "cancel" ? ConversationSteeringInputState.Interrupted : ConversationSteeringInputState.AppliedAtBoundary, receipt.State);
        var history = await fixture.Tasks.GetMessagesAsync(taskId, fixture.Token);
        Assert.Equal(outcome == "cancel" ? 0 : 1, history.Count(message => message.Id == ackId));
        if (outcome != "cancel") Assert.Contains("等待这个工具结果后继续", Readable(fixture.Server.Requests[1]));
        Assert.Equal("审批期间保留的草稿", workspace.ComposerText);
        Assert.False(input.IsAccepting);
        Assert.False(approval.IsPending);
        Assert.Empty(workspace.Queue.Items);
    }, TestContext.Current.CancellationToken);

    private static Task<WorkspaceModelSelectionFixture> CreateAsync(bool project) => WorkspaceModelSelectionFixture.CreateAsync(
        pauseResponse: true, withProject: project,
        configure: services => services.AddSingleton<IConversationSteeringRepository, SqliteConversationSteeringRepository>());

    private static string Readable(JsonElement request) => JsonSerializer.Serialize(request,
        new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private static string WriteCall(JsonElement request, string path)
    {
        var name = request.GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("name").GetString())
            .Single(value => value?.StartsWith("filesystem_write_file__", StringComparison.Ordinal) == true);
        return JsonSerializer.Serialize(new
        {
            type = "response.output_item.done",
            item = new { type = "function_call", call_id = "steered-write", name,
                arguments = JsonSerializer.Serialize(new { path, content = "steered-content" }) }
        });
    }
}
