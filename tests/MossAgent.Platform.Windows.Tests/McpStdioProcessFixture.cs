using System.Diagnostics;
using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Mcp;
using MossAgent.Platform.Windows.Processes;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class McpStdioProcessFixture : IAsyncDisposable
{
    private readonly CancellationTokenSource _timeout = CancellationTokenSource.CreateLinkedTokenSource(
        TestContext.Current.CancellationToken);
    private readonly List<Process> _processes = [];

    public McpStdioProcessFixture(string mode = "echo", bool child = true)
    {
        Root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        File.WriteAllText(Path.Combine(Root, "child.ps1"),
            "[IO.File]::WriteAllText('child.pid.tmp', [string]$PID); "
            + "[IO.File]::Move('child.pid.tmp', 'child.pid', $true); Start-Sleep -Seconds 60; "
            + "[IO.File]::WriteAllText('late.txt', 'unexpected descendant write')");
        _timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var script = Path.Combine(directory.FullName, "tests", "fixtures", "mcp-lifecycle-server.ps1");
        Profile = new McpServerProfile(Guid.NewGuid(), "lifecycle", McpTransportKind.Stdio, "pwsh",
            JsonSerializer.Serialize(new[] { "-NoProfile", "-File", script, "-FixtureArgument", "参数 with spaces" }),
            Root, JsonSerializer.Serialize(new Dictionary<string, string?>
            {
                ["MOSSAGENT_MCP_FIXTURE_MODE"] = mode,
                ["MOSSAGENT_MCP_FIXTURE_CHILD"] = child ? "1" : "0",
                ["MOSSAGENT_MCP_FIXTURE_VALUE"] = "环境 🚀"
            }), null, "{}", null, true);
        Client = new StdioMcpClient(Profile, new WindowsTaskProcessLauncher());
    }

    public string Root { get; }
    public CancellationToken Token => _timeout.Token;
    public McpServerProfile Profile { get; }
    public StdioMcpClient Client { get; }

    public async Task<IReadOnlyList<Process>> BindProcessesAsync()
    {
        foreach (var name in new[] { "server.pid", "child.pid" })
        {
            var path = Path.Combine(Root, name);
            string content;
            do
            {
                await Task.Delay(15, Token);
                content = await ReadProcessMarkerAsync(path);
            } while (!int.TryParse(content, out _));
            var process = Process.GetProcessById(int.Parse(content));
            _ = process.Handle;
            _processes.Add(process);
        }
        return _processes.ToArray();
    }

    private async Task<string> ReadProcessMarkerAsync(string path)
    {
        try { return File.Exists(path) ? await File.ReadAllTextAsync(path, Token) : string.Empty; }
        catch (IOException exception) when ((exception.HResult & 0xffff) is 32 or 33) { return string.Empty; }
    }

    public Task<McpCallResult> CallAsync(string message, CancellationToken? token = null) =>
        Client.CallToolAsync("echo", JsonSerializer.SerializeToElement(new { message }), token ?? Token);

    public async Task WaitForFileAsync(string name)
    {
        while (!File.Exists(Path.Combine(Root, name))) await Task.Delay(15, Token);
    }

    public async Task AssertExitedAsync(IReadOnlyList<Process> processes)
    {
        foreach (var process in processes)
            await process.WaitForExitAsync(Token).WaitAsync(TimeSpan.FromSeconds(5), Token);
        Assert.All(processes, process => Assert.True(process.HasExited));
        Assert.False(File.Exists(Path.Combine(Root, "late.txt")));
    }

    public async ValueTask DisposeAsync()
    {
        try { await Client.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10)); }
        finally
        {
            foreach (var process in _processes)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
                process.Dispose();
            }
            _timeout.Dispose();
            await WindowsProcessTestDirectory.DeleteAsync(Root);
        }
    }
}
