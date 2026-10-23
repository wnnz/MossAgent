using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class PendingAuthorizationConcurrencyTests
{
    [Fact]
    public async Task CancellingOneValidationDoesNotCancelOrReleaseAnotherOperationsRoots()
    {
        await using var fixture = await PendingAuthorizationRootFixture.CreateAsync();
        var first = new TaskCompletionSource<IRootIdentitySession>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<IRootIdentitySession>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstReleased = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var opened = 0;
        fixture.UseReader((_, _) =>
        {
            var index = Interlocked.Increment(ref opened);
            if (index == 1) firstEntered.TrySetResult();
            if (index == 2) entered.TrySetResult();
            return index == 1 ? first.Task : second.Task;
        });
        using var firstCancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var firstRun = fixture.Monitor.ValidateAsync(fixture.Project, firstCancellation.Token);
        Task secondRun = Task.CompletedTask;
        try
        {
            await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
            secondRun = fixture.Monitor.ValidateAsync(fixture.Project, fixture.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
            Assert.Equal(2, (await fixture.PeerResources.ListOutstandingAsync(fixture.Project.Id, fixture.Token)).Count);
            await firstCancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstRun.WaitAsync(TimeSpan.FromSeconds(2), fixture.Token));
            Assert.False(secondRun.IsCompleted, $"Second validation state: {secondRun.Status}; exception: {secondRun.Exception}");
            first.SetResult(fixture.NativeSession(() => firstReleased.TrySetResult()));
            await firstReleased.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
            await WaitForCountAsync(fixture, 1);
            Assert.False(secondRun.IsCompleted, $"Second validation state: {secondRun.Status}; exception: {secondRun.Exception}");
            second.SetResult(fixture.NativeSession(() => { }));
            await secondRun.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
            Assert.Empty(await fixture.PeerResources.ListOutstandingAsync(fixture.Project.Id, fixture.Token));
            Assert.True((await fixture.DrainAsync()).IsDrained);
        }
        finally
        {
            first.TrySetResult(fixture.NativeSession(() => firstReleased.TrySetResult()));
            second.TrySetResult(fixture.NativeSession(() => { }));
            try { await firstRun.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token); }
            catch (OperationCanceledException) when (firstCancellation.IsCancellationRequested) { }
            await secondRun.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
        }
    }

    [Fact]
    public async Task PollingReusesTheOwningReservationInsteadOfWritingANewLeaseEachTick()
    {
        await using var fixture = await PendingAuthorizationRootFixture.CreateAsync();
        await using var lease = await fixture.Monitor.AcquireAsync(fixture.Project, fixture.Token);
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        fixture.UseReader((_, _) =>
        {
            if (Interlocked.Increment(ref reads) == 2) observed.TrySetResult();
            return Task.FromResult(fixture.NativeSession(() => { }));
        });
        await observed.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
        Assert.False(lease.IsRevoked);
        Assert.Equal(1, await fixture.CountRegistrationsAsync());
        Assert.Single(await fixture.PeerResources.ListOutstandingAsync(fixture.Project.Id, fixture.Token));
    }

    private static async Task WaitForCountAsync(PendingAuthorizationRootFixture fixture, int expected)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        while ((await fixture.PeerResources.ListOutstandingAsync(fixture.Project.Id, deadline.Token)).Count != expected)
            await Task.Delay(15, deadline.Token);
    }
}
