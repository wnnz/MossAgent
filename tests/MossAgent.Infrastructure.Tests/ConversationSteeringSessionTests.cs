using MossAgent.Application.Conversations;
using MossAgent.Application.Models;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ConversationSteeringSessionTests
{
    [Fact]
    public async Task ValidationPrecedesApplicationAndStopRetainsUnappliedInput()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var session = Assert.IsType<ConversationSteeringSession>(await ConversationSteeringSession.OpenAsync(
            fixture.Repository, fixture.Task, fixture.Token, fixture.Token));
        var ack = await session.AcceptAsync(session.RunId, Guid.NewGuid(), "must fit in full", null, fixture.Token);
        await Assert.ThrowsAsync<PinnedModelContextException>(() => session.ApplyAtBoundaryAsync("previous response", true,
            _ => throw new PinnedModelContextException(), fixture.Token));
        Assert.Equal(ack.Input, Assert.Single((await fixture.InputsAsync()).Items));
        Assert.Empty(await fixture.Workspaces.GetMessagesAsync(fixture.Task.Id, fixture.Token));
        Assert.True(session.IsAccepting);
        await session.StopAsync(fixture.Token);
        Assert.False(session.IsAccepting);
        Assert.Equal(ConversationSteeringInputState.Interrupted, Assert.Single((await fixture.InputsAsync()).Items).State);
        await session.StopAsync(fixture.Token);
        await Assert.ThrowsAsync<ConversationSteeringException>(() => session.AcceptAsync(session.RunId, Guid.NewGuid(), "late", null, fixture.Token));
    }

    [Fact]
    public async Task SameRunInputIsAppliedOnceAndOnlyAnEmptyBoundarySealsCompletion()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var session = Assert.IsType<ConversationSteeringSession>(await ConversationSteeringSession.OpenAsync(
            fixture.Repository, fixture.Task, fixture.Token, fixture.Token));
        await Assert.ThrowsAsync<ConversationSteeringException>(() => session.AcceptAsync(Guid.NewGuid(), Guid.NewGuid(), "stale", null, fixture.Token));
        var ack = await session.AcceptAsync(session.RunId, Guid.NewGuid(), "next model input", null, fixture.Token);
        var validated = false;
        var applied = Assert.IsType<ConversationSteeringApplication>(await session.ApplyAtBoundaryAsync("streamed response", true, inputs =>
        {
            Assert.Equal(ack.Input, Assert.Single(inputs));
            validated = true;
        }, fixture.Token));
        Assert.True(validated);
        Assert.True(session.IsAccepting);
        Assert.Equal(session.RunId, applied.Run.Id);
        Assert.Equal(ack.Input.Id, Assert.Single(applied.Messages).Id);
        Assert.Equal("streamed response", applied.PrecedingAssistant!.Content);
        Assert.Equal(ConversationSteeringInputState.AppliedAtBoundary, Assert.Single(applied.Inputs).State);
        Assert.Null(await session.ApplyAtBoundaryAsync(null, true, _ => throw new InvalidOperationException(), fixture.Token));
        Assert.False(session.IsAccepting);
        Assert.Equal(2, (await fixture.Workspaces.GetMessagesAsync(fixture.Task.Id, fixture.Token)).Count);
        await session.StopAsync(fixture.Token);
    }

    [Fact]
    public async Task RunCancellationRefusesNewInputButCleanupStillCommitsInterruption()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var session = Assert.IsType<ConversationSteeringSession>(await ConversationSteeringSession.OpenAsync(
            fixture.Repository, fixture.Task, runCancellation.Token, fixture.Token));
        await session.AcceptAsync(session.RunId, Guid.NewGuid(), "accepted before cancel", null, fixture.Token);
        runCancellation.Cancel();
        Assert.False(session.IsAccepting);
        await Assert.ThrowsAsync<ConversationSteeringException>(() => session.AcceptAsync(session.RunId, Guid.NewGuid(), "cancelled", null, fixture.Token));
        await session.StopAsync(fixture.Token);
        Assert.Equal(ConversationSteeringInputState.Interrupted, Assert.Single((await fixture.InputsAsync()).Items).State);
        Assert.Empty(await fixture.Workspaces.GetMessagesAsync(fixture.Task.Id, fixture.Token));
    }
}
