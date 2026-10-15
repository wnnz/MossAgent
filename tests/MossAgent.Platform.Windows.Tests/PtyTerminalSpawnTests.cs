using MossAgent.Platform.Windows.Terminal;
using Porta.Pty;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class PtyTerminalSpawnTests
{
    [Fact]
    public async Task NativeOptionsUseCancellableIoAndDoNotLoadUserProfiles()
    {
        PtyOptions? captured = null;
        var connection = new PtyTestConnection();
        await using var session = await PtyTerminalSession.StartAsync(Path.GetTempPath(), _ => { }, TestContext.Current.CancellationToken,
            (options, _) => { captured = options; return Task.FromResult<IPtyConnection>(connection); });
        Assert.NotNull(captured);
        Assert.True(captured.UseAsyncIo);
        Assert.Contains("-NoProfile", captured.CommandLine);
        Assert.Contains("-NoExit", captured.CommandLine);
        Assert.Contains("[Console]::InputEncoding = [Text.UTF8Encoding]::new($false)", captured.CommandLine.Last(), StringComparison.Ordinal);
        Assert.Contains("[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)", captured.CommandLine.Last(), StringComparison.Ordinal);
        Assert.Equal(Path.GetTempPath(), captured.Cwd);
    }

    [Fact]
    public async Task NativeSpawnLateCancellationDisposesConnectionBeforeReturning()
    {
        var connection = new PtyTestConnection();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PtyTerminalSession.StartAsync(Path.GetTempPath(), _ => { }, cancellation.Token,
            (_, _) => { cancellation.Cancel(); return Task.FromResult<IPtyConnection>(connection); }));
        Assert.Equal(1, connection.DisposeCount);
    }
}
