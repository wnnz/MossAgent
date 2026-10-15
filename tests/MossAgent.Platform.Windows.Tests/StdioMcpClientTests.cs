using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Mcp;
using MossAgent.Platform.Windows.Processes;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class StdioMcpClientTests
{
    [Fact]
    public async Task Client_InitializesListsAndCallsTool()
    {
        var root = FindRepositoryRoot();
        var script = Path.Combine(root, "tests", "fixtures", "mcp-echo-server.ps1");
        var arguments = JsonSerializer.Serialize(new[] { "-NoProfile", "-File", script });
        var profile = new McpServerProfile(
            Guid.NewGuid(), "fixture", McpTransportKind.Stdio, "pwsh", arguments,
            root, "{}", null, "{}", null, true);
        await using var client = new StdioMcpClient(profile, new WindowsTaskProcessLauncher());
        var cancellationToken = TestContext.Current.CancellationToken;

        await client.InitializeAsync(cancellationToken);
        var tools = await client.ListToolsAsync(cancellationToken);
        using var document = JsonDocument.Parse("{\"message\":\"hello\"}");
        var result = await client.CallToolAsync(
            "echo", document.RootElement.Clone(), cancellationToken);

        var tool = Assert.Single(tools);
        Assert.Equal("echo", tool.Name);
        Assert.True(tool.IsReadOnly);
        Assert.False(result.IsError);
        Assert.Equal("hello", result.Content);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "MossAgent.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException();
    }
}
