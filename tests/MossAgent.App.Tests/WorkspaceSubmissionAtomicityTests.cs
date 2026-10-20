using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceSubmissionAtomicityTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ProviderCannotObserveCommittedMessageWithItsUnconsumedDraft(bool withProject, bool existing)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(withProject: withProject, pauseResponse: true);
        var workspace = fixture.Workspace;
        if (existing)
        {
            var now = DateTimeOffset.UtcNow;
            var task = new AgentTask(Guid.NewGuid(), workspace.Catalog.SelectedProject?.Id, "existing",
                ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed, now, now, null);
            await fixture.Tasks.SaveTaskAsync(task, fixture.Token);
            await workspace.LoadAsync();
            workspace.Catalog.SelectedTask = task;
            await workspace.Drafts.LoadCurrentAsync();
        }
        workspace.ComposerText = "  persist exactly once  ";
        await workspace.Drafts.FlushAsync();
        var source = workspace.Drafts.Scope;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        var saved = await fixture.Drafts.GetAsync(source, fixture.Token);
        Assert.NotNull(saved);
        Assert.Empty(saved.Text);
        var committed = Assert.Single(await fixture.Tasks.GetTasksAsync(source.ProjectId, fixture.Token));
        Assert.Single(await fixture.Tasks.GetMessagesAsync(committed.Id, fixture.Token), message => message.Role == MessageRole.User);
        await workspace.ShutdownAsync();
    }
}
