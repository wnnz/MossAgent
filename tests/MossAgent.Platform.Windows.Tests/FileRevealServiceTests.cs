using MossAgent.Platform.Windows.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class FileRevealServiceTests
{
    [Fact]
    public async Task PresentationRunsOnAnOwnedStaThread_NotTheCallingThread()
    {
        var callingThread = Environment.CurrentManagedThreadId;
        var canonical = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "synthetic file, & data.txt"));
        var validations = 0;
        var service = new WindowsFileRevealService((path, validate, token) =>
        {
            Assert.Equal(canonical, path);
            Assert.NotEqual(callingThread, Environment.CurrentManagedThreadId);
            Assert.Equal(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
            Assert.True(Thread.CurrentThread.IsBackground);
            Assert.Equal(1, validations);
            validate();
            token.ThrowIfCancellationRequested();
        });
        await service.RevealAsync(canonical, () => validations++, TestContext.Current.CancellationToken);
        Assert.Equal(2, validations);
        await Assert.ThrowsAsync<ArgumentException>(() => service.RevealAsync("relative.txt", () => { }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PendingNativePresentationRetainsOwnershipUntilReturning()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var service = new WindowsFileRevealService((_, _, token) =>
        {
            started.SetResult();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            token.ThrowIfCancellationRequested();
        });
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var operation = service.RevealAsync(Path.GetFullPath("synthetic.txt"), () => { }, cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        cancellation.Cancel();
        try { Assert.False(operation.IsCompleted); }
        finally { release.Set(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
    }

    [Fact]
    public async Task CancelledOrFailedPresentation_PropagatesWithoutRetry()
    {
        var calls = 0;
        var service = new WindowsFileRevealService((_, _, _) => { calls++; throw new InvalidOperationException("synthetic"); });
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RevealAsync(Path.GetFullPath("file.txt"), () => { }, cancellation.Token));
        Assert.Equal(0, calls);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RevealAsync(Path.GetFullPath("file.txt"), () => { }, TestContext.Current.CancellationToken));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task InvalidatedPathCannotReachNativeSelection()
    {
        var calls = 0;
        var service = new WindowsFileRevealService((_, _, _) => calls++);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RevealAsync(Path.GetFullPath("file.txt"),
            () => throw new UnauthorizedAccessException("path denied"), TestContext.Current.CancellationToken));
        Assert.Equal(0, calls);
    }
}
