using Avalonia.Headless;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class McpUiRecoveryTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task OfflineToolFailureRequiresExplicitReconnectAndNewApprovedRun(bool dark, bool compact) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await McpUiRecoveryFixture.CreateAsync(dark, compact);
            fixture.Server.Offline = true;
            fixture.Start();
            await fixture.WaitUntilAsync(() => fixture.Main.Approval.IsPending);
            Assert.Equal(0, fixture.Server.Calls);
            Assert.Equal(fixture.ToolName, fixture.Main.Approval.ToolName);
            fixture.PressCommand(fixture.Main.Approval.ApproveCommand);
            await fixture.WaitUntilAsync(() => fixture.Model.Requests.Count == 2 && !fixture.Main.Workspace.IsRunning);
            Assert.Equal(1, fixture.Server.Calls);
            await fixture.AssertPersistedAsync("tool_failed", true);
            await fixture.ReconnectAsync();
            Assert.Single(fixture.Manager.Errors);
            Assert.DoesNotContain(fixture.Tools.Descriptors, descriptor => descriptor.Name == fixture.ToolName);
            var header = Assert.Single(fixture.Window.GetVisualDescendants().OfType<AppSettingsPageHeader>());
            Assert.Equal(fixture.Main.Mcp.Status, header.Status);
            Assert.DoesNotContain("fixture-secret-marker", header.Status);
            Assert.True(header.IsStatusError);
            fixture.SaveReview("offline");
            Assert.Equal(2, fixture.Model.Requests.Count);
            Assert.Equal(1, fixture.Server.Calls);
            fixture.Server.Offline = false;
            await fixture.ReconnectAsync();
            Assert.Empty(fixture.Manager.Errors);
            Assert.Contains(fixture.Tools.Descriptors, descriptor => descriptor.Name == fixture.ToolName);
            Assert.False(header.IsStatusError);
            fixture.SaveReview("reconnected");
            Assert.Equal(2, fixture.Model.Requests.Count);
            Assert.Equal(1, fixture.Server.Calls);
            fixture.Start();
            await fixture.WaitUntilAsync(() => fixture.Main.Approval.IsPending);
            fixture.PressCommand(fixture.Main.Approval.ApproveCommand);
            await fixture.WaitUntilAsync(() => fixture.Model.Requests.Count == 4 && !fixture.Main.Workspace.IsRunning);
            await fixture.Model.Completion.WaitAsync(fixture.Token);
            await fixture.AssertPersistedAsync(null, true);
            McpUiRecoveryReview.AssertVisibleResult(fixture);
            fixture.SaveReview("completed");
            Assert.Equal(2, fixture.Server.Calls);
            Assert.True(fixture.Server.CustomHeaderObserved);
            Assert.True(fixture.Server.SessionHeaderObserved);
            Assert.Equal(3, fixture.Server.Initializations);
            await fixture.ReopenAsync();
            await fixture.AssertPersistedAsync(null, true);
            McpUiRecoveryReview.AssertVisibleResult(fixture);
            fixture.SaveReview("restored");
            Assert.Equal(4, fixture.Model.Requests.Count);
            Assert.Equal(2, fixture.Server.Calls);
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, false, ApprovalPolicy.AskEveryTime)]
    [InlineData(true, true, ApprovalPolicy.AskEveryTime)]
    [InlineData(false, true, ApprovalPolicy.ReadOnly)]
    [InlineData(true, false, ApprovalPolicy.ReadOnly)]
    public Task RejectionNeverReachesServerAndDoesNotLeakOfflineBody(bool dark, bool compact, ApprovalPolicy policy) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await McpUiRecoveryFixture.CreateAsync(dark, compact);
            fixture.Server.Offline = true;
            fixture.Start(policy);
            if (policy == ApprovalPolicy.AskEveryTime)
            {
                await fixture.WaitUntilAsync(() => fixture.Main.Approval.IsPending);
                fixture.PressCommand(fixture.Main.Approval.DenyCommand);
            }
            await fixture.WaitUntilAsync(() => fixture.Model.Requests.Count == 2 && !fixture.Main.Workspace.IsRunning);
            Assert.Equal(0, fixture.Server.Calls);
            await fixture.AssertPersistedAsync(policy == ApprovalPolicy.ReadOnly ? "capability_denied" : "approval_denied", false);
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task CancelReconnectPreventsLateRegistrationAndAllowsRetry(bool dark, bool compact) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await McpUiRecoveryFixture.CreateAsync(dark, compact);
            fixture.Server.HoldInitialization = true;
            fixture.Main.SelectSettingsTabCommand.Execute("3");
            fixture.PressCommand(fixture.Main.Mcp.ReloadCommand);
            var execution = fixture.Main.Mcp.ReloadCommand.ExecutionTask!;
            await fixture.Server.InitializationStarted.Task.WaitAsync(fixture.Token);
            ProviderUiWorkflowInput.Flush(fixture.Window);
            Assert.False(fixture.Main.Mcp.CanEdit);
            fixture.PressCommand(fixture.Main.Mcp.CancelReloadCommand);
            await execution.WaitAsync(fixture.Token);
            Assert.Contains("已取消", fixture.Main.Mcp.Status);
            Assert.False(fixture.Main.Mcp.HasStatusError);
            Assert.True(fixture.Main.Mcp.CanEdit);
            fixture.Server.HoldInitialization = false;
            fixture.Server.ReleaseInitialization.TrySetResult();
            await fixture.Server.HeldInitializationFinished.Task.WaitAsync(fixture.Token);
            ProviderUiWorkflowInput.Flush(fixture.Window);
            Assert.DoesNotContain(fixture.Tools.Descriptors, descriptor => descriptor.Name == fixture.ToolName);
            Assert.Empty(fixture.Model.Requests);
            await fixture.ReconnectAsync();
            Assert.Empty(fixture.Manager.Errors);
            Assert.Contains(fixture.Tools.Descriptors, descriptor => descriptor.Name == fixture.ToolName);
            Assert.Empty(fixture.Model.Requests);
            Assert.Equal(0, fixture.Server.Calls);
        }, TestContext.Current.CancellationToken);
}
