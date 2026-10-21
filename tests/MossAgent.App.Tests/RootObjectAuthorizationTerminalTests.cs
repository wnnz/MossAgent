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

public sealed class RootObjectAuthorizationTerminalTests
{
    [Fact]
    public async Task ActualTerminalPinsItsRootUntilExplicitReauthorizationStopsOwnedSession()
    {
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(false, "unused");
        var services = fixture.Services;
        var project = fixture.Workspace.Catalog.SelectedProject!;
        var task = new AgentTask(Guid.NewGuid(), project.Id, "root pin terminal", ApprovalPolicy.FullAccess,
            AgentTaskStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
        var repository = services.GetRequiredService<IWorkspaceRepository>();
        await repository.SaveTaskAsync(task, fixture.Token);
        var context = services.GetRequiredService<WorkspaceTaskPanelCoordinator>().CreateContext(task, project);
        var executor = services.GetRequiredService<IToolExecutor>();
        var start = await executor.ExecuteAsync(new("root-pin-start", "terminal.start", JsonSerializer.SerializeToElement(new { })), context, fixture.Token);
        Assert.True(start.IsSuccess, start.Summary);
        Assert.Throws<IOException>(() => Directory.Move(fixture.ProjectRoot, fixture.ProjectRoot + "-moved"));
        var confirmation = Assert.IsAssignableFrom<IProjectAuthorizationConfirmation>(repository);
        var saved = await confirmation.ConfirmRootObjectsAsync(project, fixture.Token);
        Assert.Equal(2, saved.AuthorizationRevision);
        var states = Assert.IsAssignableFrom<ITerminalStateSource>(services.GetRequiredService<ITerminalSessionManager>());
        while (states.GetSnapshot(task.Id)?.State is not (TerminalSessionState.Stopped or TerminalSessionState.Exited))
            await Task.Delay(15, fixture.Token);
        while (true)
        {
            fixture.Token.ThrowIfCancellationRequested();
            try { Directory.Move(fixture.ProjectRoot, fixture.ProjectRoot + "-moved"); break; }
            catch (IOException) { await Task.Delay(15, fixture.Token); }
        }
        var refused = await executor.ExecuteAsync(new("root-pin-denied", "terminal.start", JsonSerializer.SerializeToElement(new { })), context, fixture.Token);
        Assert.Equal("workspace_authorization_changed", refused.ErrorCode);
        var audits = await services.GetRequiredService<IToolExecutionAuditReader>().GetRecentAsync(task.Id, 10, fixture.Token);
        Assert.Equal(2, audits.Count);
        Assert.All(audits, audit => Assert.NotNull(audit.CompletedAt));
        Assert.Empty(fixture.Server.Requests);
    }
}
