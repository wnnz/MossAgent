using MossAgent.Application.Conversations;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ConversationSteeringStopRaceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StopDuringAcceptanceValidationDrainsItWithoutCommittingNewInput(bool cancelRun)
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        using var run = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var block = false;
        var session = Assert.IsType<ConversationSteeringSession>(await ConversationSteeringSession.OpenAsync(
            fixture.Repository, fixture.Task, run.Token, fixture.Token, async _ =>
            {
                if (block) { entered.TrySetResult(); await release.Task.WaitAsync(fixture.Token); }
            }));
        await session.AcceptAsync(session.RunId, Guid.NewGuid(), "accepted before stop", null, fixture.Token);
        block = true;
        var accepting = session.AcceptAsync(session.RunId, Guid.NewGuid(), "must not commit during stop", null, fixture.Token);
        await entered.Task.WaitAsync(fixture.Token);
        if (cancelRun) run.Cancel();
        var stopping = session.StopAsync(fixture.Token);
        Assert.False(stopping.IsCompleted);
        Assert.False(session.IsAccepting);
        release.SetResult();
        var failure = await Record.ExceptionAsync(async () => await accepting);
        Assert.True(failure is ConversationSteeringException or OperationCanceledException, failure?.ToString());
        await stopping.WaitAsync(fixture.Token);
        var receipt = Assert.Single((await fixture.InputsAsync()).Items);
        Assert.Equal("accepted before stop", receipt.Text);
        Assert.Equal(ConversationSteeringInputState.Interrupted, receipt.State);
        Assert.False((await fixture.Repository.GetRunAsync(session.TaskId, session.RunId, fixture.Token))!.IsOpen);
        Assert.Empty(await fixture.Workspaces.GetMessagesAsync(fixture.Task.Id, fixture.Token));
    }
}
