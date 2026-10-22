using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Attachments;
using MossAgent.Application.Conversations;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceSteeringDraftTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task AcceptanceAndDraftConsumptionCommitTogetherAndAnExactRetryPreservesTheNextDraft() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await CreateAsync();
        var session = await StartAsync(fixture);
        var drafts = fixture.Workspace.Drafts;
        drafts.Text = "accepted composer guidance";
        await drafts.FlushAsync();
        var submission = drafts.CreateSteeringSubmission(session, drafts.Scope, drafts.Text, [], []);
        var id = Guid.NewGuid();
        var accepted = await submission.SaveAsync(session, session.RunId, id, fixture.Token);
        Assert.False(accepted.AlreadyAccepted);
        Assert.Equal(string.Empty, drafts.Text);
        Assert.Equal(string.Empty, (await fixture.Drafts.GetAsync(drafts.Scope, fixture.Token))!.Text);
        drafts.Text = "next draft";
        await drafts.FlushAsync();
        Assert.True((await submission.SaveAsync(session, session.RunId, id, fixture.Token)).AlreadyAccepted);
        Assert.Equal("next draft", drafts.Text);
        Assert.Equal("next draft", (await fixture.Drafts.GetAsync(drafts.Scope, fixture.Token))!.Text);
        fixture.Server.ReleaseResponses();
        await fixture.WaitUntilAsync(() => !fixture.Workspace.IsRunning);
        Assert.Single(await fixture.Tasks.GetMessagesAsync(session.TaskId, fixture.Token), message => message.Id == id);
        Assert.Equal(2, fixture.Server.Requests.Count);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task TypingAndSwitchingConversationsWhileAcceptanceWaitsPreserveBothDrafts() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await CreateAsync();
        var session = await StartAsync(fixture);
        var drafts = fixture.Workspace.Drafts;
        drafts.Text = "captured guidance";
        await drafts.FlushAsync();
        var scope = drafts.Scope;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = Assert.IsType<SteeringAcceptanceProbe>(fixture.Steering);
        probe.BeforeAccept = async token => { entered.TrySetResult(); await release.Task.WaitAsync(token); };
        var submission = drafts.CreateSteeringSubmission(session, scope, drafts.Text, [], []);
        var saving = submission.SaveAsync(session, session.RunId, Guid.NewGuid(), fixture.Token);
        await entered.Task.WaitAsync(fixture.Token);
        drafts.Text = "next text for active conversation";
        fixture.Workspace.Catalog.NewChatCommand.Execute(null);
        fixture.Workspace.ComposerText = "foreground new chat draft";
        release.SetResult();
        var accepted = await saving;
        Assert.Equal("captured guidance", accepted.Input.Text);
        Assert.Equal("foreground new chat draft", fixture.Workspace.ComposerText);
        await drafts.FlushAsync();
        Assert.Equal("next text for active conversation", (await fixture.Drafts.GetAsync(scope, fixture.Token))!.Text);
        Assert.Equal("foreground new chat draft", (await fixture.Drafts.GetAsync(drafts.Scope, fixture.Token))!.Text);
        fixture.Runs.Cancel(session.TaskId);
        await fixture.WaitUntilAsync(() => !fixture.Runs.IsRunning(session.TaskId));
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task StaleRunOrExternalDraftConflictDoesNotConsumeTheDraftOrInsertAnInput(bool externalConflict) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await CreateAsync();
        var session = await StartAsync(fixture);
        var drafts = fixture.Workspace.Drafts;
        drafts.Text = "keep local guidance";
        await drafts.FlushAsync();
        var scope = drafts.Scope;
        var saved = (await fixture.Drafts.GetAsync(scope, fixture.Token))!;
        if (externalConflict)
            Assert.True(await fixture.Drafts.SaveAsync([new(scope, "external draft", saved.Revision)], fixture.Token));
        var submission = drafts.CreateSteeringSubmission(session, scope, drafts.Text, [], []);
        if (externalConflict)
        {
            await Assert.ThrowsAsync<ConversationDraftConflictException>(() => submission.SaveAsync(session, session.RunId, Guid.NewGuid(), fixture.Token));
            Assert.True(drafts.IsConflict);
        }
        else await Assert.ThrowsAsync<ConversationSteeringException>(() => submission.SaveAsync(session, Guid.NewGuid(), Guid.NewGuid(), fixture.Token));
        Assert.Equal("keep local guidance", drafts.Text);
        Assert.Empty((await fixture.Steering.GetInputsAsync(session.TaskId, long.MaxValue, 64, fixture.Token)).Items);
        Assert.Equal(externalConflict ? "external draft" : "keep local guidance", (await fixture.Drafts.GetAsync(scope, fixture.Token))!.Text);
        if (externalConflict) await drafts.KeepLocalCommand.ExecuteAsync(null);
        fixture.Runs.Cancel(session.TaskId);
        await fixture.WaitUntilAsync(() => !fixture.Runs.IsRunning(session.TaskId));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task AttachmentsRequireMatchingSnapshotsAndAnotherDraftStoreCannotBeUsed() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await CreateAsync();
        var session = await StartAsync(fixture);
        var drafts = fixture.Workspace.Drafts;
        using var other = new WorkspaceComposerDraftsViewModel(new TestConversationDraftRepository());
        Assert.Throws<InvalidOperationException>(() => other.CreateSteeringSubmission(session, drafts.Scope, "wrong store", [], []));
        drafts.Text = "text with an attachment";
        var path = Path.Combine(fixture.DataRoot, "attachment.txt");
        var attachmentId = Guid.NewGuid();
        drafts.SetAttachments(drafts.Scope, [new(attachmentId, path,
            "attachment.txt", "content", 7, WorkspaceAttachmentAuthorization.CreateSelection(path))]);
        var invalid = drafts.CreateSteeringSubmission(session, drafts.Scope, drafts.Text, [attachmentId], []);
        var refused = await Assert.ThrowsAsync<ConversationSteeringException>(
            () => invalid.SaveAsync(session, session.RunId, Guid.NewGuid(), fixture.Token));
        Assert.Equal("SteeringInvalid", refused.Code);
        Assert.Equal("text with an attachment", drafts.Text);
        Assert.Single(drafts.Attachments);
        Assert.Empty((await fixture.Steering.GetInputsAsync(session.TaskId, long.MaxValue, 64, fixture.Token)).Items);
        drafts.SetAttachments(drafts.Scope, []);
        fixture.Runs.Cancel(session.TaskId);
        await fixture.WaitUntilAsync(() => !fixture.Runs.IsRunning(session.TaskId));
    }, TestContext.Current.CancellationToken);

    private static Task<WorkspaceModelSelectionFixture> CreateAsync() => WorkspaceModelSelectionFixture.CreateAsync(
        pauseResponse: true, withProject: false, configure: services =>
        {
            services.AddSingleton<SqliteConversationSteeringRepository>();
            services.AddSingleton<IConversationSteeringRepository>(provider => new SteeringAcceptanceProbe(
                provider.GetRequiredService<SqliteConversationSteeringRepository>()));
        });

    private static async Task<ConversationSteeringSession> StartAsync(WorkspaceModelSelectionFixture fixture)
    {
        fixture.Workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        Assert.True(fixture.Runs.TryGet(fixture.Workspace.Catalog.SelectedTask!.Id, out var state));
        await fixture.Workspace.Drafts.LoadCurrentAsync();
        return Assert.IsType<ConversationSteeringSession>(state!.Steering);
    }
}
