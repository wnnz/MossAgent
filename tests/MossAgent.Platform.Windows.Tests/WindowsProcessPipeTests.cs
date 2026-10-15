using System.IO.Pipes;
using MossAgent.Platform.Windows.Processes;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class WindowsProcessPipeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static TimeSpan Deadline => TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnlyTheParentEndpointIsAsynchronousAndOnlyTheChildEndpointIsInherited(bool childReads)
    {
        using var pipe = WindowsProcessPipe.Create(childReads);
        Assert.Equal(childReads, WindowsPipeHandleProbe.IsInherited(pipe.ReadHandle));
        Assert.Equal(!childReads, WindowsPipeHandleProbe.IsInherited(pipe.WriteHandle));
        using var reader = Assert.IsType<StreamReader>(pipe.OpenReader());
        using var writer = Assert.IsType<StreamWriter>(pipe.OpenWriter());
        Assert.Equal(!childReads, Assert.IsAssignableFrom<PipeStream>(reader.BaseStream).IsAsync);
        Assert.Equal(childReads, Assert.IsAssignableFrom<PipeStream>(writer.BaseStream).IsAsync);

        var reading = reader.ReadLineAsync(Token).AsTask();
        await writer.WriteLineAsync("Unicode 管道 🚀".AsMemory(), Token);

        Assert.Equal("Unicode 管道 🚀", await reading.WaitAsync(Deadline, Token));
    }

    [Fact]
    public async Task CancelledParentReadDoesNotRequireChildOutputOrClosureAndCanBeRetried()
    {
        using var pipe = WindowsProcessPipe.Create();
        using var reader = pipe.OpenReader();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var reading = reader.ReadLineAsync(cancellation.Token).AsTask();
        Assert.False(reading.IsCompleted);

        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading.WaitAsync(Deadline, Token));

        using var writer = pipe.OpenWriter();
        await writer.WriteLineAsync("retained".AsMemory(), Token);
        Assert.Equal("retained", await reader.ReadLineAsync(Token).AsTask().WaitAsync(Deadline, Token));
    }

    [Fact]
    public async Task CancelledParentWriteDoesNotRequireChildReadOrClosure()
    {
        using var pipe = WindowsProcessPipe.Create(childReads: true);
        using var writer = Assert.IsType<StreamWriter>(pipe.OpenWriter());
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var writing = writer.BaseStream.WriteAsync(new byte[1024 * 1024], cancellation.Token).AsTask();
        try
        {
            Assert.False(writing.IsCompleted);
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writing.WaitAsync(Deadline, Token));
            Assert.True(WindowsPipeHandleProbe.IsInherited(pipe.ReadHandle));
        }
        finally { pipe.Dispose(); }
    }

    [Fact]
    public async Task DisposingTheOwnerDoesNotCloseTransferredEndpoints()
    {
        using var pipe = WindowsProcessPipe.Create();
        using var reader = pipe.OpenReader();
        using var writer = pipe.OpenWriter();
        pipe.Dispose();

        await writer.WriteLineAsync("transferred".AsMemory(), Token);

        Assert.Equal("transferred", await reader.ReadLineAsync(Token).AsTask().WaitAsync(Deadline, Token));
    }
}
