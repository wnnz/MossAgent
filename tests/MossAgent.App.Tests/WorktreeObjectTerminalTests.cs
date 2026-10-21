using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Authorization;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorktreeObjectTerminalTests
{
    [Fact]
    public async Task TerminalRetainsWorktreeAfterToolReturnAndProjectRevocationStopsItsOwner()
    {
        await using var owner = await WorkspaceWorktreeGitFixture.CreateAsync();
        var fixture = owner.Fixture;
        var task = await WorktreeObjectRuntimeTests.CreateTaskAsync(owner, policy: ApprovalPolicy.FullAccess);
        await using var services = AppComposition.CreateServices(fixture.DataRoot);
        var context = services.GetRequiredService<WorkspaceTaskPanelCoordinator>().CreateContext(task, owner.Project);
        var executor = services.GetRequiredService<IToolExecutor>();
        var start = await executor.ExecuteAsync(new("worktree-terminal-start", "terminal.start", JsonSerializer.SerializeToElement(new { })), context, fixture.Token);
        Assert.True(start.IsSuccess, start.Summary);
        Assert.Throws<IOException>(() => Directory.Move(task.WorktreePath!, task.WorktreePath + "-moved"));
        var repository = services.GetRequiredService<IWorkspaceRepository>();
        await Assert.IsAssignableFrom<IProjectAuthorizationConfirmation>(repository).ConfirmRootObjectsAsync(owner.Project, fixture.Token);
        var states = Assert.IsAssignableFrom<ITerminalStateSource>(services.GetRequiredService<ITerminalSessionManager>());
        while (states.GetSnapshot(task.Id)?.State is not (TerminalSessionState.Stopped or TerminalSessionState.Exited))
            await Task.Delay(15, fixture.Token);
        await RootObjectTestDirectory.MoveWhenReadersFinishAsync(task.WorktreePath!, task.WorktreePath + "-moved", fixture.Token);
        Directory.Move(task.WorktreePath + "-moved", task.WorktreePath!);
        var refused = await executor.ExecuteAsync(new("worktree-terminal-revoked", "terminal.start", JsonSerializer.SerializeToElement(new { })), context, fixture.Token);
        Assert.Equal("workspace_authorization_changed", refused.ErrorCode);
        Assert.Empty(fixture.Server.Requests);
    }
}
