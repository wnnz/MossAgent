using Avalonia.Headless;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceSteeringRootExpansionTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task AddingRootsPreservesFrozenOrdinaryRunButCannotAuthorizeNewOrPendingGuidance(bool pending) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true);
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        var taskId = workspace.Catalog.SelectedTask!.Id;
        Assert.True(fixture.Runs.TryGet(taskId, out var original));
        var project = original!.Project!;
        var roots = project.AuthorizedDirectories.ToArray();
        if (pending)
        {
            workspace.ComposerText = "guidance before roots changed";
            await workspace.Steering.SubmitCommand.ExecuteAsync(null);
            Assert.Single(workspace.Steering.History.Items);
        }
        await fixture.Tasks.SaveProjectAsync(project with { AuthorizedDirectories = roots.Concat([fixture.DataRoot]).ToArray() }, fixture.Token);
        workspace.ComposerText = "preserved guidance after root addition";
        await workspace.Steering.SubmitCommand.ExecuteAsync(null);
        Assert.True(workspace.Steering.HasNotice);
        Assert.Contains("授权", workspace.Steering.Notice);
        Assert.Equal(roots, original.Project!.AuthorizedDirectories);
        Assert.False(original.IsCancellationRequested);
        fixture.Server.ReleaseResponses();
        await fixture.WaitUntilAsync(() => !workspace.IsRunning);

        Assert.Single(fixture.Server.Requests);
        Assert.Equal(pending ? AgentTaskStatus.Failed : AgentTaskStatus.Completed, workspace.Catalog.SelectedTask!.Status);
        Assert.Equal("preserved guidance after root addition", workspace.ComposerText);
        Assert.Empty(workspace.Queue.Items);
        var receipts = (await fixture.Steering.GetInputsAsync(taskId, long.MaxValue, 64, fixture.Token)).Items;
        if (pending) Assert.Equal(ConversationSteeringInputState.Interrupted, Assert.Single(receipts).State);
        else Assert.Empty(receipts);
        Assert.DoesNotContain(await fixture.Tasks.GetMessagesAsync(taskId, fixture.Token),
            message => message.Content.StartsWith("guidance before", StringComparison.Ordinal)
                || message.Content == "preserved guidance after root addition");
    }, TestContext.Current.CancellationToken);
}
