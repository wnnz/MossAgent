using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Terminal;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class PtyTerminalPipelineBoundaryTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static TimeSpan Deadline => TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(ApprovalPolicy.ReadOnly, "terminal.write", "capability_denied")]
    [InlineData(ApprovalPolicy.ReadOnly, "terminal.stop", "capability_denied")]
    [InlineData(ApprovalPolicy.AskEveryTime, "terminal.write", "approval_denied")]
    [InlineData(ApprovalPolicy.AskEveryTime, "terminal.stop", "approval_denied")]
    public async Task DeniedWriteOrStopCannotMutateAnActiveSession(ApprovalPolicy policy, string tool, string error)
    {
        var connection = new PtyTestConnection();
        await using var manager = new PtyTerminalSessionManager((_, output, _) =>
            Task.FromResult(new PtyTerminalSession(connection, new CancellationTokenSource(), output)));
        var task = Guid.NewGuid();
        var root = Path.GetTempPath();
        await manager.StartAsync(task, root, Token);
        var snapshot = manager.GetSnapshot(task);
        var executor = new ToolExecutor(new ToolRegistry([new TerminalWriteTool(manager, new AuthorizedPathResolver()), new TerminalStopTool(manager)]),
            new PolicyApprovalService((_, _, _) => ValueTask.FromResult(false)));
        var arguments = tool == "terminal.write" ? JsonSerializer.SerializeToElement(new { input = "denied" })
            : JsonSerializer.SerializeToElement(new { });
        var result = await executor.ExecuteAsync(new ToolRequest("denied", tool, arguments),
            new ToolExecutionContext(task, root, [root], policy), Token);
        Assert.Equal(error, result.ErrorCode);
        Assert.Equal(0, connection.WriterStream.Length);
        Assert.Equal(0, connection.DisposeCount);
        Assert.Equal(snapshot, manager.GetSnapshot(task));
        await manager.WriteAsync(task, "retained", Token);
        Assert.Equal(8, connection.WriterStream.Length);
    }

    [Fact]
    public async Task PipelineTimeoutRetainsLateSpawnUntilItIsCleanedAndAllowsExplicitRetry()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<PtyTerminalSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new PtyTestConnection();
        var calls = 0;
        await using var manager = new PtyTerminalSessionManager(async (_, output, cancellationToken) =>
        {
            if (++calls > 1) return new PtyTerminalSession(new PtyTestConnection(), new CancellationTokenSource(), output);
            using var registration = cancellationToken.Register(() => cancelled.TrySetResult());
            entered.SetResult();
            return await pending.Task;
        });
        var root = Path.GetTempPath();
        var task = Guid.NewGuid();
        var executor = new ToolExecutor(new ToolRegistry([new TerminalStartTool(manager, new AuthorizedPathResolver())]),
            new PolicyApprovalService((_, _, _) => ValueTask.FromResult(true)));
        var execution = executor.ExecuteAsync(new ToolRequest("timeout", "terminal.start", JsonSerializer.SerializeToElement(new { })),
            new ToolExecutionContext(task, root, [root], ApprovalPolicy.FullAccess, Timeout: TimeSpan.FromSeconds(2)), Token);
        try
        {
            await entered.Task.WaitAsync(Deadline, Token);
            await cancelled.Task.WaitAsync(Deadline, Token);
            Assert.False(execution.IsCompleted);
            pending.SetResult(new PtyTerminalSession(connection, new CancellationTokenSource(), _ => { }));
            var result = await execution.WaitAsync(Deadline, Token);
            Assert.Equal("tool_timeout", result.ErrorCode);
            Assert.Equal(1, connection.DisposeCount);
            await manager.StartAsync(task, root, Token);
            Assert.Equal(2, calls);
        }
        finally
        {
            if (!pending.Task.IsCompleted) pending.TrySetResult(new PtyTerminalSession(connection, new CancellationTokenSource(), _ => { }));
            await execution.WaitAsync(Deadline, Token);
        }
    }
}
