using MossAgent.Domain;
using MossAgent.Mcp;
using MossAgent.Tools.Abstractions.Processes;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class McpFailureClassificationTests
{
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task HttpRejectedWithoutChallenge_ExplainsCredentialAction(int status)
    {
        await using var server = new WorkspaceMcpHttpServer { RejectionStatus = status };
        await using var manager = CreateManager(HttpProfile(server.Endpoint));

        await manager.ReloadAsync(TestContext.Current.CancellationToken);

        var error = Assert.Single(manager.Errors).Value;
        Assert.Contains($"（{status}）", error, StringComparison.Ordinal);
        Assert.Contains("凭据", error, StringComparison.Ordinal);
        Assert.Contains("headers", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HttpRejectedWithChallenge_NamesSchemeAndReloadAction()
    {
        await using var server = new WorkspaceMcpHttpServer
        {
            RejectionStatus = 401,
            RejectionChallenge = "Bearer realm=\"mcp\""
        };
        await using var manager = CreateManager(HttpProfile(server.Endpoint));

        await manager.ReloadAsync(TestContext.Current.CancellationToken);

        var error = Assert.Single(manager.Errors).Value;
        Assert.Contains("Bearer", error, StringComparison.Ordinal);
        Assert.Contains("（401）", error, StringComparison.Ordinal);
        Assert.Contains("重载", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HttpSessionLostAfterAccept_ReportsSessionInvalid()
    {
        await using var server = new WorkspaceMcpHttpServer
        {
            RejectionStatus = 404,
            RejectionRequiresSession = true
        };
        await using var manager = CreateManager(HttpProfile(server.Endpoint));

        await manager.ReloadAsync(TestContext.Current.CancellationToken);

        var error = Assert.Single(manager.Errors).Value;
        Assert.True(server.SessionHeaderObserved);
        Assert.Contains("会话已失效（404）", error, StringComparison.Ordinal);
        Assert.Contains("重新加载", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StdioProcessExit_ReportsExitCodeAndStartupGuidance()
    {
        var process = new StubTaskProcess { ExitCode = 3, StandardInput = TextWriter.Null };
        await using var manager = CreateManager(StdioProfile(), () => process);

        await manager.ReloadAsync(TestContext.Current.CancellationToken);

        var error = Assert.Single(manager.Errors).Value;
        Assert.Contains("退出码 3", error, StringComparison.Ordinal);
        Assert.Contains("启动命令", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StdioStreamEndWhileProcessRuns_KeepsGenericDisconnectMessage()
    {
        var process = new StubTaskProcess
        {
            StandardInput = TextWriter.Null,
            Wait = token => Task.Delay(Timeout.InfiniteTimeSpan, token)
        };
        await using var manager = CreateManager(StdioProfile(), () => process);

        await manager.ReloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal("连接已断开，请检查 MCP 服务进程或网络。", Assert.Single(manager.Errors).Value);
    }

    private static McpManager CreateManager(McpServerProfile profile, Func<ITaskProcess>? process = null) =>
        new(new McpReloadProbeRepository { Items = [profile] },
            new McpHttpClientFactory(new ProxyReloadProbeRepository()),
            new ToolRegistry([]),
            new RecordingTaskProcessLauncher(process ?? DeniedLaunch));

    private static ITaskProcess DeniedLaunch() =>
        throw new InvalidOperationException("A denied request must not launch a process.");

    private static McpServerProfile HttpProfile(Uri endpoint) => new(
        Guid.NewGuid(), "workflow", McpTransportKind.StreamableHttp,
        null, "[]", null, "{}", endpoint, "{}", null, true);

    private static McpServerProfile StdioProfile() => new(
        Guid.NewGuid(), "workflow", McpTransportKind.Stdio,
        "fixture-server", "[]", null, "{}", null, "{}", null, true);
}
