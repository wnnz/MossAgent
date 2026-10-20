using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceSubmissionBindingTests
{
    [Fact]
    public async Task ProductionCompositionBindsOnlyItsOwnDraftStore()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var submissions = Assert.IsAssignableFrom<IConversationSubmissionRepository>(fixture.Tasks);
        Assert.True(submissions.SupportsDrafts(fixture.Drafts));
        Assert.False(submissions.SupportsDrafts(new TestConversationDraftRepository()));
        await using var other = await WorkspaceModelSelectionFixture.CreateAsync();
        Assert.False(submissions.SupportsDrafts(other.Drafts));
    }

    [Fact]
    public async Task AnInMemoryEditorDoesNotConsumeTheProductionEditorsDraft()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        fixture.Workspace.ComposerText = "retained production editor";
        await fixture.Workspace.Drafts.FlushAsync();
        var scope = fixture.Workspace.Drafts.Scope;
        using var runs = new WorkspaceRunTracker();
        await using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs);
        await workspace.LoadAsync();
        workspace.ComposerText = "separate in-memory editor";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !workspace.IsRunning);
        await workspace.Drafts.FlushAsync();
        Assert.Empty(workspace.ComposerText);
        Assert.Equal("retained production editor", (await fixture.Drafts.GetAsync(scope, fixture.Token))!.Text);
        Assert.Equal("retained production editor", fixture.Workspace.ComposerText);
    }
}
