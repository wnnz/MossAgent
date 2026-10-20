using MossAgent.Application.Conversations;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ConversationSteeringEmptyBoundaryTests
{
    [Fact]
    public async Task EmptyBoundaryGuardNeverAuthorizesAcceptanceOrPendingApplication()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var changed = false;
        var emptyCalls = 0;
        var session = Assert.IsType<ConversationSteeringSession>(await ConversationSteeringSession.OpenAsync(
            fixture.Repository, fixture.Task, fixture.Token, fixture.Token,
            _ => changed ? Task.FromException(new ConversationSteeringException("SteeringScopeChanged")) : Task.CompletedTask,
            validateEmptyBoundary: _ => { emptyCalls++; return Task.CompletedTask; }));
        Assert.Null(await session.ApplyAtBoundaryAsync(null, false, _ => throw new InvalidOperationException(), fixture.Token));
        Assert.Equal(1, emptyCalls);
        var accepted = await session.AcceptAsync(session.RunId, Guid.NewGuid(), "pending before change", null, fixture.Token);
        changed = true;

        await Assert.ThrowsAsync<ConversationSteeringException>(() => session.AcceptAsync(session.RunId,
            Guid.NewGuid(), "refuse changed scope", null, fixture.Token));
        await Assert.ThrowsAsync<ConversationSteeringException>(() => session.ApplyAtBoundaryAsync(
            null, true, _ => throw new InvalidOperationException(), fixture.Token));

        Assert.Equal(1, emptyCalls);
        Assert.Equal(accepted.Input, Assert.Single((await fixture.InputsAsync()).Items));
        Assert.Empty(await fixture.Workspaces.GetMessagesAsync(fixture.Task.Id, fixture.Token));
        await session.StopAsync(fixture.Token);
        Assert.Equal(ConversationSteeringInputState.Interrupted, Assert.Single((await fixture.InputsAsync()).Items).State);
    }

    [Fact]
    public async Task EmptyBoundaryDenialCannotSealTheRunAndDefaultGuardRemainsStrict()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var changed = false;
        var session = Assert.IsType<ConversationSteeringSession>(await ConversationSteeringSession.OpenAsync(
            fixture.Repository, fixture.Task, fixture.Token, fixture.Token,
            _ => changed ? Task.FromException(new ConversationSteeringException("SteeringScopeChanged")) : Task.CompletedTask));
        changed = true;

        await Assert.ThrowsAsync<ConversationSteeringException>(() => session.ApplyAtBoundaryAsync(
            null, true, _ => throw new InvalidOperationException(), fixture.Token));

        Assert.True(session.IsAccepting);
        Assert.True((await fixture.Repository.GetRunAsync(fixture.Task.Id, session.RunId, fixture.Token))!.IsOpen);
        await session.StopAsync(fixture.Token);
    }
}
