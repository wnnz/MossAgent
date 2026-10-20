using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteConversationSteeringBoundaryTests
{
    [Fact]
    public async Task OnlyTheCurrentPendingPrefixIsAppliedAndCannotBeReplayed()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var run = await fixture.OpenAsync();
        var first = await fixture.AcceptAsync(run, "first");
        var second = await fixture.AcceptAsync(run, "second");
        var tool = new ConversationMessage(Guid.NewGuid(), run.TaskId, MessageRole.Tool, "completed tool", DateTimeOffset.UtcNow.AddMinutes(1), "call-1");
        await fixture.Workspaces.AppendMessageAsync(tool, fixture.Token);
        Assert.Null(await fixture.Repository.ApplyAsync(run, [first.Id], null, fixture.Token));
        run = await fixture.ReloadAsync(run);
        Assert.Null(await fixture.Repository.ApplyAsync(run, [second.Id], null, fixture.Token));
        var assistant = new ConversationMessage(Guid.NewGuid(), run.TaskId, MessageRole.Assistant, "streamed once", DateTimeOffset.UtcNow);
        Assert.NotNull(await fixture.Repository.ApplyAsync(run, [first.Id, second.Id], assistant, fixture.Token));
        var messages = await fixture.Workspaces.GetMessagesAsync(run.TaskId, fixture.Token);
        Assert.Equal(new[] { tool.Id, assistant.Id, first.Id, second.Id }, messages.Select(message => message.Id));
        Assert.True(messages[1].CreatedAt > tool.CreatedAt);
        Assert.Null(await fixture.Repository.ApplyAsync(run, [first.Id, second.Id], assistant, fixture.Token));
        Assert.Equal(4, (await fixture.Workspaces.GetMessagesAsync(run.TaskId, fixture.Token)).Count);
        Assert.All((await fixture.InputsAsync()).Items, input =>
        {
            Assert.Equal(ConversationSteeringInputState.AppliedAtBoundary, input.State);
            Assert.NotNull(input.AppliedAt);
            Assert.Null(input.InterruptedAt);
        });
        Assert.Equal(3, (await fixture.ReloadAsync(run)).Revision);
    }

    [Fact]
    public async Task MessageFailureRollsBackAssistantInputsAndReceiptTransitions()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var run = await fixture.OpenAsync();
        var first = await fixture.AcceptAsync(run, "first");
        var second = await fixture.AcceptAsync(run, "rejected input");
        run = await fixture.ReloadAsync(run);
        await fixture.ExecuteAsync("CREATE TRIGGER reject_boundary BEFORE INSERT ON messages WHEN NEW.content='rejected input' BEGIN SELECT RAISE(ABORT,'synthetic boundary failure'); END;");
        var assistant = new ConversationMessage(Guid.NewGuid(), run.TaskId, MessageRole.Assistant, "retain once", DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Repository.ApplyAsync(run, [first.Id, second.Id], assistant, fixture.Token));
        Assert.Empty(await fixture.Workspaces.GetMessagesAsync(run.TaskId, fixture.Token));
        Assert.Equal(run, await fixture.ReloadAsync(run));
        Assert.All((await fixture.InputsAsync()).Items, input => Assert.Equal(ConversationSteeringInputState.Pending, input.State));
        await fixture.ExecuteAsync("DROP TRIGGER reject_boundary;");
        Assert.NotNull(await fixture.Repository.ApplyAsync(run, [first.Id, second.Id], assistant, fixture.Token));
        Assert.Equal(3, (await fixture.Workspaces.GetMessagesAsync(run.TaskId, fixture.Token)).Count);
    }

    [Fact]
    public async Task NormalSealingRefusesPendingAndInterruptionNeverCreatesMessages()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var run = await fixture.OpenAsync();
        await fixture.AcceptAsync(run);
        Assert.False(await fixture.Repository.CloseAsync(run, true, fixture.Token));
        run = await fixture.ReloadAsync(run);
        Assert.False(await fixture.Repository.CloseAsync(run, false, fixture.Token));
        Assert.True(await fixture.Repository.CloseAsync(run, true, fixture.Token));
        Assert.False((await fixture.ReloadAsync(run)).IsOpen);
        Assert.False(await fixture.Repository.CloseAsync(run, true, fixture.Token));
        Assert.Equal(ConversationSteeringInputState.Interrupted, Assert.Single((await fixture.InputsAsync()).Items).State);
        Assert.Empty(await fixture.Workspaces.GetMessagesAsync(run.TaskId, fixture.Token));
        Assert.Null(await fixture.Repository.AcceptAsync(run.TaskId, run.Id, Guid.NewGuid(), "late", null, fixture.Token));
        Assert.Null(await fixture.Repository.OpenAsync(fixture.Task, run.Id, fixture.Token));
        var fresh = await fixture.OpenAsync();
        Assert.NotEqual(run.Id, fresh.Id);
        Assert.Empty(await fixture.Workspaces.GetMessagesAsync(run.TaskId, fixture.Token));
    }
}
