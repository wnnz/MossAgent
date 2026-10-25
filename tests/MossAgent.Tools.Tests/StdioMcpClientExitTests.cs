using MossAgent.Domain;
using MossAgent.Mcp;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions.Processes;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class StdioMcpClientExitTests
{
    [Fact]
    public async Task Initialize_WhenProcessAlreadyExited_FaultsPendingWithExitCode()
    {
        var process = new StubTaskProcess { ExitCode = 3, StandardInput = TextWriter.Null };
        await using var client = CreateClient(process);

        var exception = await Assert.ThrowsAsync<McpStdioExitException>(
            () => client.InitializeAsync(TestContext.Current.CancellationToken));

        Assert.Equal(3, exception.ExitCode);
    }

    [Fact]
    public async Task Initialize_WhenStreamEndsWhileProcessRuns_FaultsWithGenericDisconnect()
    {
        var process = new StubTaskProcess
        {
            StandardInput = TextWriter.Null,
            Wait = token => Task.Delay(Timeout.InfiniteTimeSpan, token)
        };
        await using var client = CreateClient(process);

        var exception = await Assert.ThrowsAnyAsync<IOException>(
            () => client.InitializeAsync(TestContext.Current.CancellationToken));

        Assert.IsType<IOException>(exception);
    }

    private static StdioMcpClient CreateClient(ITaskProcess process)
    {
        var profile = new McpServerProfile(Guid.NewGuid(), "fixture", McpTransportKind.Stdio,
            "fixture-server", "[]", null, "{}", null, "{}", null, true);
        return new StdioMcpClient(profile, new RecordingTaskProcessLauncher(() => process));
    }
}
