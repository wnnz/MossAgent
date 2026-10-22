using MossAgent.Application.Agent;
using MossAgent.Application.Attachments;
using MossAgent.Application.Conversations;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ConversationSteeringAttachmentBoundaryTests
{
    [Fact]
    public async Task BoundaryAddsTheStoredTextAttachmentContextBeforeCommitting()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var paths = new AppDataPaths(fixture.Project.PrimaryDirectory);
        var store = new ConversationAttachmentStore(new TaskArtifactPaths(paths));
        var inputId = Guid.NewGuid();
        var source = WriteSource(fixture, "guide.txt", "prefer small diffs");
        var snapshots = await store.SaveAsync(fixture.Task.Id, inputId,
            [new WorkspaceAttachmentContent(source, "guide.txt", "prefer small diffs", 17)], fixture.Token);
        var session = await OpenAsync(fixture, store);
        var (update, draftAttachmentId) = await DraftAsync(fixture);
        await session.AcceptAsync(session.RunId, inputId, "guide with file", update,
            fixture.Token, snapshots, [draftAttachmentId]);

        IReadOnlyList<ModelMessage> prepared = [];
        var result = Assert.IsType<AgentSteeringApplication>(await session.ApplyAtBoundaryAsync(null, false, messages =>
        {
            prepared = messages;
        }, fixture.Token));

        var message = Assert.Single(prepared);
        Assert.Equal(ModelRole.User, message.Role);
        Assert.StartsWith("guide with file", message.Content);
        Assert.Contains("workspace_attachments", message.Content);
        Assert.Contains("prefer small diffs", message.Content);
        Assert.Equal("guide with file", Assert.Single(result.Applied.Messages).Content);
        Assert.Equal(snapshots, Assert.Single(await fixture.Workspaces.GetMessagesAsync(fixture.Task.Id, fixture.Token)).Attachments);
        await session.StopAsync(fixture.Token);
    }

    [Fact]
    public async Task TamperedSnapshotFailsTheBoundaryWithoutApplyingTheInput()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var paths = new AppDataPaths(fixture.Project.PrimaryDirectory);
        var store = new ConversationAttachmentStore(new TaskArtifactPaths(paths));
        var inputId = Guid.NewGuid();
        var source = WriteSource(fixture, "tampered.txt", "original");
        var snapshots = await store.SaveAsync(fixture.Task.Id, inputId,
            [new WorkspaceAttachmentContent(source, "tampered.txt", "original", 8)], fixture.Token);
        var session = await OpenAsync(fixture, store);
        var (update, draftAttachmentId) = await DraftAsync(fixture);
        await session.AcceptAsync(session.RunId, inputId, "guide with file", update,
            fixture.Token, snapshots, [draftAttachmentId]);
        var snapshotFile = Path.Combine(new TaskArtifactPaths(paths).GetTaskDirectory(fixture.Task.Id), "attachments",
            inputId.ToString("N"), Assert.Single(snapshots).Id.ToString("N") + ".txt");
        await File.WriteAllTextAsync(snapshotFile, "replaced content", fixture.Token);

        var failure = await Assert.ThrowsAsync<ConversationSteeringException>(() => session.ApplyAtBoundaryAsync(
            null, false, _ => { }, fixture.Token));

        Assert.Equal("SteeringAttachmentsUnavailable", failure.Code);
        Assert.Empty(await fixture.Workspaces.GetMessagesAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(ConversationSteeringInputState.Pending, Assert.Single((await fixture.InputsAsync()).Items).State);
        await session.StopAsync(fixture.Token);
    }

    private static async Task<(ConversationDraftUpdate Update, Guid AttachmentId)> DraftAsync(
        ConversationSteeringTestFixture fixture)
    {
        var drafts = new SqliteConversationDraftRepository(fixture.Connections,
            new AppDataPaths(fixture.Project.PrimaryDirectory));
        var scope = new ConversationDraftScope(fixture.Task.ProjectId, fixture.Task.Id);
        var attachmentId = Guid.NewGuid();
        var item = new ConversationDraftAttachment(attachmentId, Path.Combine(fixture.Project.PrimaryDirectory, "selected.txt"),
            "selected.txt", "selected", 8, WorkspaceAttachmentAuthorization.Create(fixture.Project), "text/plain", null);
        Assert.True(await drafts.SaveAsync([new(scope, "guide with file", 0, [item])], fixture.Token));
        var saved = Assert.IsType<ConversationDraft>(await drafts.GetAsync(scope, fixture.Token));
        return (new(scope, string.Empty, saved.Revision, []), attachmentId);
    }

    private static string WriteSource(ConversationSteeringTestFixture fixture, string name, string content)
    {
        var path = Path.Combine(fixture.Project.PrimaryDirectory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static async Task<ConversationSteeringSession> OpenAsync(ConversationSteeringTestFixture fixture,
        IConversationAttachmentStore store)
    {
        var model = new ModelProfile(Guid.NewGuid(), Guid.NewGuid(), "vision", "vision", 8192, 1024,
            "medium", null, null, true, false, false, true, "{}");
        var session = await ConversationSteeringSession.OpenAsync(fixture.Repository, fixture.Task,
            fixture.Token, fixture.Token, attachments: store, model: model);
        return Assert.IsType<ConversationSteeringSession>(session);
    }
}
