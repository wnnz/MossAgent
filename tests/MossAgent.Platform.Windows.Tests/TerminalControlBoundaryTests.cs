using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Terminal;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class TerminalControlBoundaryTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static ToolRequest Request(string tool, Guid? session = null) => new("control", tool,
        tool == "terminal.write" ? JsonSerializer.SerializeToElement(new { input = "SENTINEL", sessionId = session?.ToString("D") })
            : JsonSerializer.SerializeToElement(new { columns = 40, rows = 10, sessionId = session?.ToString("D") }));
    private static ToolRegistry Registry(PtyTerminalSessionManager manager) => new([
        new TerminalWriteTool(manager, new AuthorizedPathResolver()), new TerminalResizeTool(manager, new AuthorizedPathResolver())]);

    [Theory]
    [InlineData("terminal.write", ApprovalPolicy.ReadOnly, "capability_denied")]
    [InlineData("terminal.resize", ApprovalPolicy.ReadOnly, "capability_denied")]
    [InlineData("terminal.write", ApprovalPolicy.AskEveryTime, "approval_denied")]
    [InlineData("terminal.resize", ApprovalPolicy.AskEveryTime, "approval_denied")]
    public async Task RefusalDoesNotMutateProcess(string tool, ApprovalPolicy policy, string error)
    {
        var resizes = 0;
        var connection = new PtyTestConnection { Resizing = (_, _) => resizes++ };
        await using var manager = new PtyTerminalSessionManager((_, output, _) => Task.FromResult(new PtyTerminalSession(connection, new(), output)));
        var context = new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], policy);
        await manager.StartAsync(context.TaskId, context.WorkingDirectory, Token);
        var before = manager.GetSnapshot(context.TaskId)!;
        var executor = new ToolExecutor(Registry(manager), new PolicyApprovalService((_, _, _) => ValueTask.FromResult(false)));
        var result = await executor.ExecuteAsync(Request(tool, before.SessionId), context, Token);
        Assert.Equal(error, result.ErrorCode);
        Assert.Equal(0, connection.WriterStream.Length);
        Assert.Equal(0, resizes);
        Assert.Equal(before, manager.GetSnapshot(context.TaskId));
    }

    [Theory]
    [InlineData("terminal.write", false)]
    [InlineData("terminal.resize", false)]
    [InlineData("terminal.write", true)]
    [InlineData("terminal.resize", true)]
    public async Task AuthorizationIsCheckedBeforeApprovalAndAgainAfterIt(string tool, bool revokeAfterApproval)
    {
        var calls = 0;
        var resizes = 0;
        var connection = new PtyTestConnection { Resizing = (_, _) => resizes++ };
        await using var manager = new PtyTerminalSessionManager((_, output, _) => Task.FromResult(new PtyTerminalSession(connection, new(), output)));
        var root = Path.GetTempPath();
        var roots = new List<string> { revokeAfterApproval ? root : Path.Combine(root, "unrelated") };
        var context = new ToolExecutionContext(Guid.NewGuid(), root, roots, ApprovalPolicy.AskEveryTime);
        await manager.StartAsync(context.TaskId, root, Token);
        var executor = new ToolExecutor(Registry(manager), new PolicyApprovalService((_, _, _) =>
        { calls++; roots.Clear(); return ValueTask.FromResult(true); }));
        var result = await executor.ExecuteAsync(Request(tool, manager.GetSnapshot(context.TaskId)!.SessionId), context, Token);
        Assert.Equal("path_denied", result.ErrorCode);
        Assert.Equal(revokeAfterApproval ? 1 : 0, calls);
        Assert.Equal(0, resizes);
        Assert.Equal(0, connection.WriterStream.Length);
    }

    [Theory]
    [InlineData("terminal.write")]
    [InlineData("terminal.resize")]
    public async Task OldApprovalCannotControlReplacementSession(string tool)
    {
        var connections = new List<PtyTestConnection>();
        var resizes = 0;
        await using var manager = new PtyTerminalSessionManager((_, output, _) =>
        {
            var connection = new PtyTestConnection { Resizing = (_, _) => resizes++ };
            connections.Add(connection);
            return Task.FromResult(new PtyTerminalSession(connection, new(), output));
        });
        var context = new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.AskEveryTime);
        await manager.StartAsync(context.TaskId, context.WorkingDirectory, Token);
        var previous = manager.GetSnapshot(context.TaskId)!;
        var executor = new ToolExecutor(Registry(manager), new PolicyApprovalService(async (_, _, _) =>
        {
            await manager.StopAsync(context.TaskId);
            await manager.StartAsync(context.TaskId, context.WorkingDirectory, Token);
            return true;
        }));
        var result = await executor.ExecuteAsync(Request(tool, previous.SessionId), context, Token);
        Assert.False(result.IsSuccess);
        Assert.NotEqual(previous.SessionId, manager.GetSnapshot(context.TaskId)!.SessionId);
        Assert.Equal(0, connections[1].WriterStream.Length);
        Assert.Equal(0, resizes);
    }

    [Theory]
    [InlineData("terminal.write", "malformed")]
    [InlineData("terminal.resize", "malformed")]
    [InlineData("terminal.write", "oversized")]
    [InlineData("terminal.resize", "oversized")]
    [InlineData("terminal.write", "cancelled")]
    [InlineData("terminal.resize", "cancelled")]
    public async Task InvalidOrCancelledRequestsDoNotAskOrMutate(string tool, string scenario)
    {
        var approvals = 0;
        var connection = new PtyTestConnection { Resizing = (_, _) => Assert.Fail("unexpected resize") };
        await using var manager = new PtyTerminalSessionManager((_, output, _) => Task.FromResult(new PtyTerminalSession(connection, new(), output)));
        var context = new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.AskEveryTime);
        await manager.StartAsync(context.TaskId, context.WorkingDirectory, Token);
        var session = scenario == "malformed" ? "invalid" : manager.GetSnapshot(context.TaskId)!.SessionId.ToString("D");
        var arguments = tool == "terminal.write" ? JsonSerializer.SerializeToElement(new { input = new string('x', scenario == "oversized" ? 32769 : 3), sessionId = session })
            : JsonSerializer.SerializeToElement(new { columns = scenario == "oversized" ? 501 : 40, rows = 10, sessionId = session });
        var executor = new ToolExecutor(Registry(manager), new PolicyApprovalService((_, _, _) => { approvals++; return ValueTask.FromResult(true); }));
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Token);
        if (scenario == "cancelled") cancelled.Cancel();
        var result = await executor.ExecuteAsync(new("invalid", tool, arguments), context, cancelled.Token);
        Assert.Equal(scenario == "cancelled" ? "tool_cancelled" : "invalid_arguments", result.ErrorCode);
        Assert.Equal(0, approvals);
        Assert.Equal(0, connection.WriterStream.Length);
        Assert.Equal(TerminalGridSize.Default, manager.GetSnapshot(context.TaskId)!.Grid);
    }
}
