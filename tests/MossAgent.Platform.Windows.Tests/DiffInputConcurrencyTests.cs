using MossAgent.Testing;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class DiffInputConcurrencyTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CancellationOrTimeoutWaitsForProcessStopBeforeInputReleaseAndPreservesOtherTask(bool statistics, bool timeout)
    {
        using var first = await DiffInputFlowFixture.CreateAsync();
        using var second = await DiffInputFlowFixture.CreateAsync();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(first.Files.Token);
        var time = new ManualToolTimeProvider();
        var process = new DiffInputHeldProcess();
        first.Processes = new DiffInputProcessLauncher { Held = process };
        process.Stopping = () => Assert.All(first.Inputs.Sessions, session => Assert.False(session.IsDisposed));
        var pending = first.ExecuteAsync(statistics, first.Files.Context with { Timeout = TimeSpan.FromSeconds(1) }, cancellation.Token, time);
        await process.Started.Task.WaitAsync(first.Files.Token);
        Assert.Equal(2, first.Inputs.Sessions.Count);
        Assert.All(first.Inputs.Sessions, session => Assert.True(File.Exists(session.Path)));
        var completed = await second.ExecuteAsync(statistics);
        Assert.True(completed.IsSuccess, completed.Summary);
        var preserved = completed.Artifacts?.Single();
        var before = preserved is null ? null : await File.ReadAllBytesAsync(preserved.Path, first.Files.Token);
        if (timeout) time.Advance(TimeSpan.FromSeconds(2));
        else await cancellation.CancelAsync();
        var result = await pending.WaitAsync(first.Files.Token);
        Assert.Equal(timeout ? "tool_timeout" : "tool_cancelled", result.ErrorCode);
        Assert.True(process.Terminated);
        Assert.All(first.Inputs.Sessions, session => { Assert.True(session.IsDisposed); Assert.False(File.Exists(session.Path)); });
        Assert.All(second.Inputs.Sessions, session => Assert.True(session.IsDisposed));
        if (preserved is not null) Assert.Equal(before, await File.ReadAllBytesAsync(preserved.Path, first.Files.Token));
        Assert.Equal(2, Directory.GetFiles(first.Files.ArtifactRoot).Length);
    }
}
