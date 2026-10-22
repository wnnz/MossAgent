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
public sealed class WorkspaceSteeringTransactionAccessTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData("project", false)]
    [InlineData("grant", false)]
    [InlineData("account", false)]
    [InlineData("binding", false)]
    [InlineData("project", true)]
    [InlineData("grant", true)]
    [InlineData("account", true)]
    [InlineData("binding", true)]
    public Task RevocationAfterPrecheckIsRejectedInsideAcceptanceOrBoundaryTransaction(string change, bool boundary) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await CreateAsync(change == "project");
            var revoke = await PrepareRevocationAsync(fixture, change);
            var workspace = fixture.Workspace;
            workspace.PrimaryActionCommand.Execute(null);
            await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
            var taskId = workspace.Catalog.SelectedTask!.Id;
            Assert.True(fixture.Runs.TryGet(taskId, out var state));
            var session = Assert.IsType<ConversationSteeringSession>(state!.Steering);
            var probe = Assert.IsType<SteeringAcceptanceProbe>(fixture.Steering);
            workspace.ComposerText = "guidance preserved across transactional revocation";
            if (boundary)
            {
                await workspace.Steering.SubmitCommand.ExecuteAsync(null);
                Assert.Single((await fixture.Steering.GetInputsAsync(taskId, long.MaxValue, 64, fixture.Token)).Items);
                probe.BeforeApply = revoke;
            }
            else
            {
                probe.BeforeAccept = revoke;
                var submission = workspace.Drafts.CreateSteeringSubmission(session, workspace.Drafts.Scope, workspace.ComposerText, [], []);
                var failure = await Assert.ThrowsAsync<ConversationSteeringException>(() =>
                    submission.SaveAsync(session, session.RunId, Guid.NewGuid(), fixture.Token));
                Assert.Equal("SteeringScopeChanged", failure.Code);
                Assert.Equal("guidance preserved across transactional revocation", workspace.ComposerText);
            }
            fixture.Server.ReleaseResponses();
            await fixture.WaitUntilAsync(() => !workspace.IsRunning);
            Assert.Single(fixture.Server.Requests);
            Assert.Equal(AgentTaskStatus.Failed, workspace.Catalog.SelectedTask!.Status);
            var receipts = (await fixture.Steering.GetInputsAsync(taskId, long.MaxValue, 64, fixture.Token)).Items;
            if (boundary)
            {
                var receipt = Assert.Single(receipts);
                Assert.Equal(ConversationSteeringInputState.Interrupted, receipt.State);
                Assert.DoesNotContain(await fixture.Tasks.GetMessagesAsync(taskId, fixture.Token), message => message.Id == receipt.Id);
            }
            else
            {
                Assert.Empty(receipts);
                await workspace.Drafts.FlushAsync();
                Assert.Equal(workspace.ComposerText, (await fixture.Drafts.GetAsync(workspace.Drafts.Scope, fixture.Token))!.Text);
            }
        }, TestContext.Current.CancellationToken);

    private static async Task<Func<CancellationToken, Task>> PrepareRevocationAsync(WorkspaceModelSelectionFixture fixture, string change)
    {
        var workspace = fixture.Workspace;
        if (change == "project")
        {
            var project = workspace.Catalog.SelectedProject!;
            return token => fixture.Tasks.SaveProjectAsync(project with { AuthorizedDirectories = [] }, token);
        }
        var account = await fixture.SourceAccounts.SaveAsync(new(Guid.NewGuid(), "transaction fixture",
            new Uri("https://enterprise.example.test/api/v3/"), "synthetic-credential", null, true), fixture.Token);
        var scope = new SourceRepositoryScope(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo");
        await fixture.SourceGrants.AddAsync(scope, fixture.Token);
        var task = await fixture.SourceChats.OpenAsync(new(Guid.NewGuid(), scope,
            new(7, new string('a', 40), new string('b', 40)), "saved snapshot"), "transaction source", fixture.Token);
        workspace.Catalog.SelectTaskCommand.Execute(task);
        await fixture.WaitUntilAsync(() => workspace.Catalog.SelectedTask?.Id == task.Id && workspace.Drafts.Scope.TaskId == task.Id);
        await workspace.Drafts.LoadCurrentAsync();
        workspace.ComposerText = "initial source question";
        await fixture.WaitUntilAsync(() => workspace.PrimaryActionCommand.CanExecute(null));
        if (change == "grant") return token => fixture.SourceGrants.RevokeAsync(scope.GrantId, token);
        if (change == "binding") return async token =>
        {
            var connections = new SqliteConnectionFactory(new AppDataPaths(fixture.DataRoot));
            await using var connection = connections.Create();
            await connection.OpenAsync(token);
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE source_review_chats SET head_revision=$head WHERE task_id=$task;";
            command.Parameters.AddWithValue("$head", new string('c', 40));
            command.Parameters.AddWithValue("$task", task.Id.ToString());
            Assert.Equal(1, await command.ExecuteNonQueryAsync(token));
        };
        return async token => { await fixture.SourceAccounts.SaveAsync(account, token); };
    }

    private static Task<WorkspaceModelSelectionFixture> CreateAsync(bool project) => WorkspaceModelSelectionFixture.CreateAsync(
        pauseResponse: true, withProject: project, configure: services =>
        {
            services.AddSingleton<SqliteConversationSteeringRepository>();
            services.AddSingleton<IConversationSteeringRepository>(provider => new SteeringAcceptanceProbe(
                provider.GetRequiredService<SqliteConversationSteeringRepository>()));
        });
}
