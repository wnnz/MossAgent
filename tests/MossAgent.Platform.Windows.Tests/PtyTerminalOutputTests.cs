using System.Text;
using MossAgent.Platform.Windows.Terminal;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class PtyTerminalOutputTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task Utf8CharactersSurviveArbitraryPipeBoundaries(int chunkSize)
    {
        const string expected = "中文测试 € 🤖 é\r\n";
        var connection = new PtyTestConnection
        { ReaderStream = new PtyChunkedReadStream(Encoding.UTF8.GetBytes(expected), chunkSize) };
        var output = new StringBuilder();
        await using var manager = new PtyTerminalSessionManager((_, publish, _) =>
            Task.FromResult(new PtyTerminalSession(connection, new CancellationTokenSource(), publish)));
        var task = Guid.NewGuid();
        manager.OutputReceived += (_, args) => { if (args.TaskId == task) output.Append(args.Output); };
        await manager.StartAsync(task, Path.GetTempPath(), TestContext.Current.CancellationToken);
        await manager.StopAsync(task);
        Assert.Equal(expected, output.ToString());
    }

    [Fact]
    public async Task FailingSubscriberDoesNotPreventAnotherSubscriberOrLaterOutput()
    {
        var connection = new PtyTestConnection
        { ReaderStream = new PtyChunkedReadStream(Encoding.UTF8.GetBytes("healthy-output"), 1) };
        var output = new StringBuilder();
        await using var manager = new PtyTerminalSessionManager((_, publish, _) =>
            Task.FromResult(new PtyTerminalSession(connection, new CancellationTokenSource(), publish)));
        manager.OutputReceived += (_, _) => throw new IOException("synthetic-observer-failure");
        manager.OutputReceived += (_, args) => output.Append(args.Output);
        var task = Guid.NewGuid();
        await manager.StartAsync(task, Path.GetTempPath(), TestContext.Current.CancellationToken);
        await manager.StopAsync(task);
        Assert.Equal("healthy-output", output.ToString());
        Assert.Equal(1, connection.DisposeCount);
    }
}
