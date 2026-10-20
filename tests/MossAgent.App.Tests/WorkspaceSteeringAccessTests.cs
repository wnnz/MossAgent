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
public sealed class WorkspaceSteeringAccessTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task SourceRevocationOrCredentialChangeRefusesNewGuidanceAndNeverReplaysAcceptedInput(bool credentials, bool alreadyAccepted) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await CreateAsync(false);
            var account = await fixture.SourceAccounts.SaveAsync(new(Guid.NewGuid(), "steering source fixture",
                new Uri("https://enterprise.example.test/api/v3/"), "synthetic-source-credential", null, true), fixture.Token);
            var scope = new SourceRepositoryScope(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo");
            await fixture.SourceGrants.AddAsync(scope, fixture.Token);
            var task = await fixture.SourceChats.OpenAsync(new(Guid.NewGuid(), scope,
                new(7, new string('a', 40), new string('b', 40)), "saved untrusted snapshot"), "source guidance", fixture.Token);
            var workspace = fixture.Workspace;
            workspace.Catalog.SelectTaskCommand.Execute(task);
            await fixture.WaitUntilAsync(() => workspace.Catalog.SelectedTask?.Id == task.Id && workspace.Drafts.Scope.TaskId == task.Id);
            await workspace.Drafts.LoadCurrentAsync();
            workspace.ComposerText = "initial source question";
            await fixture.WaitUntilAsync(() => workspace.PrimaryActionCommand.CanExecute(null));
            var session = await StartAsync(fixture);
            if (alreadyAccepted) await session.AcceptAsync(session.RunId, Guid.NewGuid(), "accepted before revocation", null, fixture.Token);
            if (credentials) await fixture.SourceAccounts.SaveAsync(account, fixture.Token);
            else await fixture.SourceGrants.RevokeAsync(scope.GrantId, fixture.Token);
            workspace.ComposerText = "guidance after revocation";
            var submission = workspace.Drafts.CreateSteeringSubmission(session, workspace.Drafts.Scope, workspace.ComposerText);
            await Assert.ThrowsAsync<SourceReviewException>(() => submission.SaveAsync(session, session.RunId, Guid.NewGuid(), fixture.Token));
            Assert.Equal("guidance after revocation", workspace.ComposerText);
            fixture.Server.ReleaseResponses();
            await fixture.WaitUntilAsync(() => !workspace.IsRunning);
            Assert.Equal(AgentTaskStatus.Failed, workspace.Catalog.SelectedTask?.Status);
            Assert.Single(fixture.Server.Requests);
            var receipts = (await fixture.Steering.GetInputsAsync(task.Id, long.MaxValue, 64, fixture.Token)).Items;
            if (alreadyAccepted)
            {
                var receipt = Assert.Single(receipts);
                Assert.Equal(ConversationSteeringInputState.Interrupted, receipt.State);
                Assert.DoesNotContain(await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token), message => message.Id == receipt.Id);
            }
            else Assert.Empty(receipts);
            await workspace.Drafts.FlushAsync();
            Assert.Equal("guidance after revocation", (await fixture.Drafts.GetAsync(workspace.Drafts.Scope, fixture.Token))!.Text);
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ProjectRootChangesRefuseAcceptanceOrBoundaryApplicationWithoutAnotherModelRequest(bool acceptFirst) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await CreateAsync(true);
        var session = await StartAsync(fixture);
        var workspace = fixture.Workspace;
        if (acceptFirst) await session.AcceptAsync(session.RunId, Guid.NewGuid(), "before scope change", null, fixture.Token);
        var project = workspace.Catalog.SelectedProject!;
        await fixture.Tasks.SaveProjectAsync(project with { AuthorizedDirectories = [fixture.DataRoot] }, fixture.Token);
        workspace.ComposerText = "preserved scope-change draft";
        var failure = await Assert.ThrowsAsync<ConversationSteeringException>(() => session.AcceptAsync(session.RunId,
            Guid.NewGuid(), "after scope change", null, fixture.Token));
        Assert.Equal("SteeringScopeChanged", failure.Code);
        fixture.Server.ReleaseResponses();
        await fixture.WaitUntilAsync(() => !workspace.IsRunning && workspace.Steering.HasNotice);
        Assert.Equal(AgentTaskStatus.Failed, workspace.Catalog.SelectedTask?.Status);
        Assert.True(workspace.Steering.IsVisible);
        Assert.Contains("授权", workspace.Steering.Notice);
        Assert.Single(fixture.Server.Requests);
        Assert.Equal("preserved scope-change draft", workspace.ComposerText);
        var receipts = (await fixture.Steering.GetInputsAsync(session.TaskId, long.MaxValue, 64, fixture.Token)).Items;
        if (acceptFirst) Assert.Equal(ConversationSteeringInputState.Interrupted, Assert.Single(receipts).State);
        else Assert.Empty(receipts);
    }, TestContext.Current.CancellationToken);

    private static Task<WorkspaceModelSelectionFixture> CreateAsync(bool project) => WorkspaceModelSelectionFixture.CreateAsync(
        pauseResponse: true, withProject: project,
        configure: services => services.AddSingleton<IConversationSteeringRepository, SqliteConversationSteeringRepository>());

    private static async Task<ConversationSteeringSession> StartAsync(WorkspaceModelSelectionFixture fixture)
    {
        fixture.Workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        Assert.True(fixture.Runs.TryGet(fixture.Workspace.Catalog.SelectedTask!.Id, out var state));
        return Assert.IsType<ConversationSteeringSession>(state!.Steering);
    }
}
