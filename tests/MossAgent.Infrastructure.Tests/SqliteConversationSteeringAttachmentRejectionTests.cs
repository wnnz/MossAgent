using MossAgent.Application.Attachments;
using MossAgent.Application.Conversations;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteConversationSteeringAttachmentRejectionTests
{
    [Fact]
    public async Task SnapshotOwnedByAnotherTaskIsRejected()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var run = await fixture.OpenAsync();
        var inputId = Guid.NewGuid();
        var foreign = new ConversationAttachment(Guid.NewGuid(), Guid.NewGuid(), inputId, "guide.txt", 4,
            new string('A', 64), "text/plain");
        var update = new ConversationDraftUpdate(new(fixture.Task.ProjectId, fixture.Task.Id), string.Empty, 0, []);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Repository.AcceptAsync(run.TaskId, run.Id,
            inputId, "guide", update, fixture.Token, null, [foreign], [Guid.NewGuid()]));
        Assert.Empty((await fixture.InputsAsync()).Items);
    }

    [Fact]
    public async Task SnapshotCountMustMatchTheConsumedComposerAttachments()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var run = await fixture.OpenAsync();
        var inputId = Guid.NewGuid();
        var snapshot = new ConversationAttachment(Guid.NewGuid(), fixture.Task.Id, inputId, "guide.txt", 4,
            new string('A', 64), "text/plain");
        var update = new ConversationDraftUpdate(new(fixture.Task.ProjectId, fixture.Task.Id), string.Empty, 0, []);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Repository.AcceptAsync(run.TaskId, run.Id,
            inputId, "guide", update, fixture.Token, null, [snapshot], []));
        Assert.Empty((await fixture.InputsAsync()).Items);
    }

    [Fact]
    public async Task DraftAttachmentOutsideTheClaimedSetIsNotSilentlyDropped()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var drafts = new SqliteConversationDraftRepository(fixture.Connections, new AppDataPaths(fixture.Project.PrimaryDirectory));
        var scope = new ConversationDraftScope(fixture.Task.ProjectId, fixture.Task.Id);
        var stamp = WorkspaceAttachmentAuthorization.Create(fixture.Project);
        var directory = fixture.Project.PrimaryDirectory;
        var first = new ConversationDraftAttachment(Guid.NewGuid(), Path.Combine(directory, "one.txt"),
            "one.txt", "one", 3, stamp, "text/plain", null);
        var second = new ConversationDraftAttachment(Guid.NewGuid(), Path.Combine(directory, "two.txt"),
            "two.txt", "two", 3, stamp, "text/plain", null);
        Assert.True(await drafts.SaveAsync([new(scope, "guide twice", 0, [first, second])], fixture.Token));
        var saved = Assert.IsType<ConversationDraft>(await drafts.GetAsync(scope, fixture.Token));
        var run = await fixture.OpenAsync();
        var inputId = Guid.NewGuid();
        var snapshot = new ConversationAttachment(Guid.NewGuid(), fixture.Task.Id, inputId, "one.txt", 3,
            new string('A', 64), "text/plain");
        var update = new ConversationDraftUpdate(scope, string.Empty, saved.Revision, []);
        Assert.Null(await fixture.Repository.AcceptAsync(run.TaskId, run.Id, inputId, "guide twice", update,
            fixture.Token, null, [snapshot], [first.Id]));
        Assert.Equal("guide twice", Assert.IsType<ConversationDraft>(await drafts.GetAsync(scope, fixture.Token)).Text);
        Assert.Empty((await fixture.InputsAsync()).Items);
    }

    [Fact]
    public async Task ImageSnapshotIsRefusedWhenTheRunModelCannotAcceptImages()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var model = new ModelProfile(Guid.NewGuid(), Guid.NewGuid(), "text-only", "text-only", 8192, 1024,
            "medium", null, null, false, false, false, true, "{}");
        var session = Assert.IsType<ConversationSteeringSession>(await ConversationSteeringSession.OpenAsync(
            fixture.Repository, fixture.Task, fixture.Token, fixture.Token, model: model));
        var inputId = Guid.NewGuid();
        var image = new ConversationAttachment(Guid.NewGuid(), fixture.Task.Id, inputId, "shot.png", 10,
            new string('C', 64), "image/png");
        var update = new ConversationDraftUpdate(new(fixture.Task.ProjectId, fixture.Task.Id), string.Empty, 0, []);
        var failure = await Assert.ThrowsAsync<ConversationSteeringException>(() => session.AcceptAsync(
            session.RunId, inputId, "look at this", update, fixture.Token, [image], [Guid.NewGuid()]));
        Assert.Equal("SteeringAttachmentModelUnsupported", failure.Code);
        Assert.Empty((await fixture.InputsAsync()).Items);
        await session.StopAsync(fixture.Token);
    }
}
