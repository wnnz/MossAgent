using System.Text.Json;
using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ApprovalTaskControlTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task Stop_RequiresCurrentRequestAndCancelsWithoutEditing() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(true, "beta\n");
        var controls = fixture.Services.GetRequiredService<MainWindowViewModel>().ApprovalTaskControl;
        Assert.False(controls.CanStop);
        fixture.Start();
        await fixture.WaitForApprovalAsync();
        var requestId = fixture.Approval.CurrentRequestId;
        Assert.True(controls.CanStop);
        Assert.False(controls.StopCommand.CanExecute(null));
        Assert.False(controls.StopCommand.CanExecute(Guid.NewGuid()));
        controls.StopCommand.Execute(Guid.NewGuid());
        Assert.True(fixture.Workspace.IsRunning);
        Assert.True(fixture.Approval.IsPending);
        controls.StopCommand.Execute(requestId);
        await fixture.WaitForCompletionAsync();
        await fixture.AssertPersistedAsync("tool_cancelled", null, AgentTaskStatus.Cancelled);
        Assert.Equal("alpha\n", await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
        Assert.Equal(2, fixture.Server.Requests.Count);
        Assert.False(controls.CanStop);
        Assert.False(controls.StopCommand.CanExecute(requestId));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task OtherTaskApproval_CannotStopSelectedRunOrReuseItsGesture() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(true, "beta\n");
        var controls = fixture.Services.GetRequiredService<MainWindowViewModel>().ApprovalTaskControl;
        using var pendingCancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var descriptor = new ToolDescriptor("synthetic.approval", "Synthetic task", "{}", ToolRiskLevel.Mutation, ToolCapability.FileWrite);
        var context = new ToolExecutionContext(Guid.NewGuid(), fixture.ProjectRoot, [fixture.ProjectRoot], ApprovalPolicy.AskEveryTime);
        var pending = fixture.Approval.RequestAsync(descriptor, new ToolRequest("other-task", descriptor.Name,
            JsonSerializer.SerializeToElement(new { })), context, pendingCancellation.Token).AsTask();
        var otherRequest = fixture.Approval.CurrentRequestId;
        try
        {
            fixture.Start();
            while (fixture.Approval.PendingCount != 2) await Task.Delay(10, fixture.Token);
            Assert.True(fixture.Workspace.HasActiveRun);
            Assert.False(controls.CanStop);
            controls.StopCommand.Execute(otherRequest);
            Assert.True(fixture.Workspace.IsRunning);
            pendingCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.NotEqual(otherRequest, fixture.Approval.CurrentRequestId);
            Assert.True(controls.CanStop);
            controls.StopCommand.Execute(otherRequest);
            Assert.True(fixture.Approval.IsPending);
            controls.StopCommand.Execute(fixture.Approval.CurrentRequestId);
            await fixture.WaitForCompletionAsync();
            await fixture.AssertPersistedAsync("tool_cancelled", null, AgentTaskStatus.Cancelled);
            Assert.Equal("alpha\n", await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
        }
        finally { pendingCancellation.Cancel(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Disposal_DetachesUpdatesAndDoesNotResolveApproval() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(true, "beta\n");
        using var controls = new ApprovalTaskControlViewModel(fixture.Approval, fixture.Workspace);
        fixture.Start();
        await fixture.WaitForApprovalAsync();
        var requestId = fixture.Approval.CurrentRequestId;
        Assert.True(controls.CanStop);
        controls.Dispose();
        var updates = 0;
        controls.PropertyChanged += (_, _) => updates++;
        Assert.False(controls.CanStop);
        Assert.False(controls.StopCommand.CanExecute(requestId));
        controls.StopCommand.Execute(requestId);
        Assert.True(fixture.Approval.IsPending);
        fixture.Approval.DenyCommand.Execute(requestId);
        await fixture.WaitForCompletionAsync();
        await fixture.AssertPersistedAsync("approval_denied", false, AgentTaskStatus.Completed);
        Assert.Equal(0, updates);
    }, TestContext.Current.CancellationToken);
}
