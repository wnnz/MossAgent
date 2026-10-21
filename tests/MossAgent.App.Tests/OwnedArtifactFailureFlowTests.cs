using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MossAgent.Application.Artifacts;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class OwnedArtifactFailureFlowTests
{
    [Theory]
    [InlineData("denied", "approval_denied")]
    [InlineData("refused", "artifact_boundary_refused")]
    [InlineData("cancelled", "tool_cancelled")]
    public async Task FailureOrCancellationAfterSnapshotsPreservesProjectDraftAndPersistedResult(string scenario, string error)
    {
        var prepared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sessions = new ObservedArtifactSessions { AsyncCompleted = async (_, index, token) =>
        {
            if (index != 2) return;
            if (scenario == "refused") throw new FileMutationSessionException("synthetic safe artifact refusal", "artifact_boundary_refused");
            if (scenario == "cancelled")
            {
                prepared.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
        } };
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(false, "beta\n", configure: services =>
            services.Replace(ServiceDescriptor.Singleton<IArtifactFileSessionFactory>(sessions)));
        fixture.Start();
        await fixture.WaitForApprovalAsync();
        Assert.Empty(sessions.Sessions);
        if (scenario == "denied") fixture.Approval.DenyCommand.Execute(fixture.Approval.CurrentRequestId);
        else fixture.Approval.ApproveCommand.Execute(fixture.Approval.CurrentRequestId);
        fixture.Workspace.ComposerText = "保留取消后的草稿";
        if (scenario == "cancelled")
        {
            await prepared.Task.WaitAsync(fixture.Token);
            fixture.Workspace.PrimaryActionCommand.Execute(null);
        }
        await fixture.WaitForCompletionAsync();
        await fixture.AssertPersistedAsync(error, scenario != "denied", scenario == "cancelled" ? AgentTaskStatus.Cancelled : AgentTaskStatus.Completed);
        Assert.Equal(scenario == "denied" ? 0 : 2, sessions.Sessions.Count);
        Assert.All(sessions.Sessions, session => { Assert.False(session.IsRetained); Assert.False(session.Stream.CanRead); });
        var directory = fixture.Services.GetRequiredService<ITaskArtifactPaths>().GetTaskDirectory(fixture.TaskId);
        if (scenario == "denied") Assert.False(Directory.Exists(directory));
        else Assert.Empty(Directory.GetFiles(directory));
        Assert.Equal("alpha\n", await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
        Assert.Equal("保留取消后的草稿", fixture.Workspace.ComposerText);
        Assert.Equal("outside sentinel", await File.ReadAllTextAsync(fixture.OutsidePath, fixture.Token));
    }

    [Fact]
    public async Task RealSnapshotCleanupFailureIsVisibleAndAuditedAfterReload()
    {
        WorkspaceFileEditingFixture? current = null;
        string? orphan = null;
        var sessions = new ObservedArtifactSessions { Completed = (session, index) =>
        {
            if (index == 1) { File.SetAttributes(session.Stream.SafeFileHandle, FileAttributes.ReadOnly); orphan = session.Path; }
            if (index == 2) File.WriteAllText(current!.FilePath, "external sentinel");
        } };
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(false, "beta\n", configure: services =>
            services.Replace(ServiceDescriptor.Singleton<IArtifactFileSessionFactory>(sessions)));
        current = fixture;
        try
        {
            fixture.Start(ApprovalPolicy.FullAccess);
            await fixture.WaitForCompletionAsync();
            await fixture.AssertPersistedAsync("artifact_cleanup_failed", true, AgentTaskStatus.Completed);
            Assert.Contains("清理未能完成", Assert.Single(fixture.Workspace.Messages, row => row.ToolCallId == "file-edit").Tool!.Summary);
            Assert.All(sessions.Sessions, session => Assert.False(session.Stream.CanRead));
            var directory = fixture.Services.GetRequiredService<ITaskArtifactPaths>().GetTaskDirectory(fixture.TaskId);
            Assert.Equal(orphan, Assert.Single(Directory.GetFiles(directory)));
            Assert.Equal("external sentinel", await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
        }
        finally { if (orphan is not null) File.SetAttributes(orphan, FileAttributes.Normal); }
    }
}
