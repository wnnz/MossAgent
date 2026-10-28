using System.Diagnostics;
using System.Text;
using MossAgent.Platform.Windows.Sandbox;
using MossAgent.Platform.Windows.Terminal;
using MossAgent.Tools.Abstractions.Processes;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

/// <summary>
/// Real ConPTY launches inside the task AppContainer: powershell.exe resolves
/// from System32 (ALL APPLICATION PACKAGES readable) so no environment gate is
/// required. Grant writes mutate the test directories' DACLs, matching
/// <see cref="SandboxTests"/>.
/// </summary>
public sealed class SandboxedPtyTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task TerminalRunsInsideContainerAndHonorsGrants()
    {
        var root = CreateDirectory();
        var outside = CreateDirectory();
        var spec = Spec(root);
        using var ledger = new WindowsSandboxGrantLedger();
        try
        {
            var output = new StringBuilder();
            var sync = new object();
            await using var session = await PtyTerminalSession.StartAsync(root,
                text => { lock (sync) output.Append(text); }, spec, ledger, Token);

            // The NT working directory lands on the authorized root even though
            // the AppContainer's PowerShell provider reports $PWD as C:\.
            var echoed = await InvokeAsync(session, sync, output,
                "[Console]::WriteLine([IO.Directory]::GetCurrentDirectory())");
            Assert.Contains(root, StripAnsi(echoed), StringComparison.OrdinalIgnoreCase);

            // FileSystem-provider cmdlets (Set-Location/Set-Content) cannot resolve
            // granted paths inside an unregistered AppContainer; raw IO can.
            var inside = Path.Combine(root, "ok.txt");
            await InvokeAsync(session, sync, output, $"[IO.File]::WriteAllText('{inside}','x')");
            Assert.Equal("x", await File.ReadAllTextAsync(inside, Token));

            var denied = Path.Combine(outside, "no.txt");
            var deniedOutput = await InvokeAsync(session, sync, output,
                $"try {{ [IO.File]::WriteAllText('{denied}','x') }} catch {{ [Console]::WriteLine('DENIED-' + $_.Exception.GetType().Name) }}");
            Assert.Contains("DENIED-", StripAnsi(deniedOutput), StringComparison.Ordinal);
            Assert.False(File.Exists(denied));

            var grandchildDenied = Path.Combine(outside, "no-grandchild.txt");
            await InvokeAsync(session, sync, output,
                $"powershell -NoProfile -Command \"[IO.File]::WriteAllText('{grandchildDenied}','x')\"");
            Assert.False(File.Exists(grandchildDenied));

            var temp = await InvokeAsync(session, sync, output, "[Console]::WriteLine($env:TMP)");
            Assert.Contains($"Packages\\{spec.SandboxKey}", StripAnsi(temp), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await WindowsProcessTestDirectory.DeleteAsync(root);
            await WindowsProcessTestDirectory.DeleteAsync(outside);
        }
    }

    [Fact]
    public async Task TerminalExitReportsCodeAndDisposeKillsTheProcessTree()
    {
        var root = CreateDirectory();
        var spec = Spec(root);
        using var ledger = new WindowsSandboxGrantLedger();
        Process? child = null;
        try
        {
            var script = $"[IO.File]::WriteAllText('{Path.Combine(root, "child.pid")}', $PID); Start-Sleep -Seconds 90";
            await File.WriteAllTextAsync(Path.Combine(root, "child.ps1"), script, Token);
            var output = new StringBuilder();
            var sync = new object();
            await using var session = await PtyTerminalSession.StartAsync(root,
                text => { lock (sync) output.Append(text); }, spec, ledger, Token);
            await InvokeAsync(session, sync, output,
                "[Diagnostics.Process]::Start('powershell.exe', '-NoProfile -ExecutionPolicy Bypass -File child.ps1')");
            child = Process.GetProcessById(int.Parse(await WaitForFileAsync(Path.Combine(root, "child.pid"))));
            _ = child.Handle;
            Assert.False(child.HasExited);

            await session.WriteAsync("exit 42\r\n", Token);
            Assert.Equal(42, await session.Termination.WaitAsync(Deadline, Token));
            await session.DisposeAsync();

            await child.WaitForExitAsync(Token).WaitAsync(Deadline, Token);
            Assert.True(child.HasExited);
        }
        finally
        {
            child?.Dispose();
            await WindowsProcessTestDirectory.DeleteAsync(root);
        }
    }

    [Fact]
    public async Task AuthorizedStartRunsARealSandboxedTerminal()
    {
        var root = CreateDirectory();
        await using var fixture = new ProjectAuthorizationPtyFixture(root);
        var output = new StringBuilder();
        try
        {
            await using var manager = new PtyTerminalSessionManager();
            var task = Guid.NewGuid();
            manager.OutputReceived += (_, args) =>
            {
                if (args.TaskId == task) lock (output) output.Append(args.Output);
            };
            await manager.StartAuthorizedAsync(fixture.Context(task), Token);
            Assert.Equal(MossAgent.Tools.Abstractions.Terminal.TerminalSessionState.Running,
                manager.GetSnapshot(task)!.State);
            var marker = "SANDBOXED-" + Guid.NewGuid().ToString("N");
            await manager.WriteAsync(task, $"[Console]::WriteLine('SANDBOXED' + '-{marker[10..]}')\r\n", Token);
            await WaitForOutputAsync(output, marker);
            await manager.StopAsync(task);
            Assert.False(File.Exists(Path.Combine(root, marker)));
        }
        finally { await WindowsProcessTestDirectory.DeleteAsync(root); }
    }

    // ConPTY output interleaves cursor/color escape sequences inside long
    // strings, so assertions must run on the text with control codes removed.
    private static string StripAnsi(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text,
            "\x1B\\[[0-9;?]*[ -/]*[@-~]|\x1B\\][^\x07]*\x07|\x1B[@-Z\\\\-_]", string.Empty);

    private static async Task<string> InvokeAsync(PtyTerminalSession session, object sync,
        StringBuilder output, string command)
    {
        // The marker must not appear verbatim in the echoed input; otherwise the
        // waiter can return on the echo before the command's output is rendered.
        var marker = "MARK-" + Guid.NewGuid().ToString("N");
        await session.WriteAsync(command + $"; [Console]::WriteLine('MARK' + '-{marker[5..]}')\r\n", Token);
        return await WaitForOutputAsync(output, marker, sync);
    }

    private static async Task<string> WaitForOutputAsync(StringBuilder output, string marker, object? sync = null)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        cancellation.CancelAfter(Deadline);
        while (true)
        {
            lock (sync ?? output)
            {
                var text = output.ToString();
                if (text.Contains(marker, StringComparison.Ordinal)) return text;
            }
            await Task.Delay(25, cancellation.Token);
        }
    }

    private static async Task<string> WaitForFileAsync(string path)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        cancellation.CancelAfter(Deadline);
        while (true)
        {
            try { return await File.ReadAllTextAsync(path, Token); }
            catch (IOException) { await Task.Delay(25, cancellation.Token); }
        }
    }

    private static ProcessIsolationSpec Spec(string root) =>
        new($"mossagent-pty-{Guid.NewGuid():N}"[..40], [root], [],
            Path.Combine(root, "sandbox-tmp"));

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
