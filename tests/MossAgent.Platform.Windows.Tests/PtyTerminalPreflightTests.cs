using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Terminal;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class PtyTerminalPreflightTests
{
    [Theory]
    [InlineData(ApprovalPolicy.ReadOnly, "capability_denied", 0)]
    [InlineData(ApprovalPolicy.AskEveryTime, "approval_denied", 1)]
    public async Task DeniedPolicyCannotCreateANativeTerminal(ApprovalPolicy policy, string error, int expectedPrompts)
    {
        var calls = 0;
        var prompts = 0;
        await using var manager = new PtyTerminalSessionManager((_, _, _) =>
        { calls++; throw new InvalidOperationException("Denied execution reached native creation."); });
        var executor = new ToolExecutor(new ToolRegistry([new TerminalStartTool(manager, new AuthorizedPathResolver())]),
            new PolicyApprovalService((_, _, _) => { prompts++; return ValueTask.FromResult(false); }));
        var root = Path.GetTempPath();
        var task = Guid.NewGuid();
        var result = await executor.ExecuteAsync(new ToolRequest("denied", "terminal.start", JsonSerializer.SerializeToElement(new { })),
            new ToolExecutionContext(task, root, [root], policy), TestContext.Current.CancellationToken);
        Assert.Equal(error, result.ErrorCode);
        Assert.Equal(expectedPrompts, prompts);
        Assert.Equal(0, calls);
        Assert.Null(manager.GetSnapshot(task));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("relative")]
    [InlineData("C:relative")]
    public async Task InvalidWorkingDirectoryCannotCallFactory(string root)
    {
        var calls = 0;
        await using var manager = new PtyTerminalSessionManager((_, _, _) =>
        { calls++; throw new InvalidOperationException(); });
        await Assert.ThrowsAnyAsync<ArgumentException>(() => manager.StartAsync(Guid.NewGuid(), root, TestContext.Current.CancellationToken));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task AlreadyCancelledStartCannotCallFactory()
    {
        var calls = 0;
        await using var manager = new PtyTerminalSessionManager((_, _, _) =>
        { calls++; throw new InvalidOperationException(); });
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.StartAsync(Guid.NewGuid(), Path.GetTempPath(), cancellation.Token));
        Assert.Equal(0, calls);
    }
}
