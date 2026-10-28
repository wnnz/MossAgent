using MossAgent.Platform.Windows.Processes;
using MossAgent.Tools.Abstractions.Processes;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

/// <summary>
/// Real AppContainer launches. These require a Windows desktop session and mutate
/// the current user's profile registry + the test directories' DACLs.
/// Commands run through powershell.exe (inbox, AppContainer-capable) because the
/// launcher quotes every argument, which cmd's /c quote-stripping rules reject.
/// </summary>
public sealed class SandboxTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Echo_RunsInsideAppContainer()
    {
        var root = CreateDirectory();
        try
        {
            var result = await RunAsync(root, root, "[Console]::Out.Write('hello')");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("hello", result.StdOut);
        }
        finally { await WindowsProcessTestDirectory.DeleteAsync(root); }
    }

    [Fact]
    public async Task ReadInsideAuthorizedRoot_Succeeds()
    {
        var root = CreateDirectory();
        try
        {
            var inside = Path.Combine(root, "inside.txt");
            await File.WriteAllTextAsync(inside, "inside-content", Token);

            var result = await RunAsync(root, root, $"[Console]::Out.Write([IO.File]::ReadAllText('{inside}'))");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("inside-content", result.StdOut);
        }
        finally { await WindowsProcessTestDirectory.DeleteAsync(root); }
    }

    [Fact]
    public async Task ReadOutsideAuthorizedRoots_IsDenied()
    {
        var root = CreateDirectory();
        var outside = CreateDirectory();
        try
        {
            var secret = Path.Combine(outside, "secret.txt");
            await File.WriteAllTextAsync(secret, "must-not-leak", Token);

            var result = await RunAsync(root, root, $"[IO.File]::ReadAllText('{secret}')");

            Assert.NotEqual(0, result.ExitCode);
            Assert.DoesNotContain("must-not-leak", result.StdOut);
        }
        finally
        {
            await WindowsProcessTestDirectory.DeleteAsync(root);
            await WindowsProcessTestDirectory.DeleteAsync(outside);
        }
    }

    [Fact]
    public async Task WriteOutsideAuthorizedRoots_IsDenied()
    {
        var root = CreateDirectory();
        var outside = CreateDirectory();
        try
        {
            var target = Path.Combine(outside, "escape.txt");

            var result = await RunAsync(root, root, $"[IO.File]::WriteAllText('{target}','x')");

            Assert.NotEqual(0, result.ExitCode);
            Assert.False(File.Exists(target));
        }
        finally
        {
            await WindowsProcessTestDirectory.DeleteAsync(root);
            await WindowsProcessTestDirectory.DeleteAsync(outside);
        }
    }

    [Fact]
    public async Task WriteInsideAuthorizedRoot_Succeeds()
    {
        var root = CreateDirectory();
        try
        {
            var target = Path.Combine(root, "written.txt");

            var result = await RunAsync(root, root, $"[IO.File]::WriteAllText('{target}','x')");

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(target));
        }
        finally { await WindowsProcessTestDirectory.DeleteAsync(root); }
    }

    [Fact]
    public async Task Grandchild_StaysInsideTheContainer()
    {
        var root = CreateDirectory();
        var outside = CreateDirectory();
        try
        {
            var secret = Path.Combine(outside, "secret.txt");
            await File.WriteAllTextAsync(secret, "must-not-leak", Token);

            var result = await RunAsync(root, root, $"cmd /c type {secret}");

            Assert.True(result.ExitCode != 0 || result.StdErr.Length > 0,
                $"expected denial, got exit={result.ExitCode} stderr=[{result.StdErr}]");
            Assert.DoesNotContain("must-not-leak", result.StdOut);
        }
        finally
        {
            await WindowsProcessTestDirectory.DeleteAsync(root);
            await WindowsProcessTestDirectory.DeleteAsync(outside);
        }
    }

    [Fact]
    public async Task TempVariables_PointAtTheContainerPrivateDirectory()
    {
        var root = CreateDirectory();
        var key = SandboxKey();
        try
        {
            var result = await RunAsync(root, root,
                "[Console]::Out.WriteLine($env:TMP); [Console]::Out.WriteLine($env:TEMP)", key);

            Assert.Equal(0, result.ExitCode);
            // Named AppContainer profiles redirect TMP/TEMP to their packaged private
            // store, overriding the launcher's own overlay.
            var packagedTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Packages", key);
            var lines = result.StdOut.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(2, lines.Length);
            foreach (var line in lines)
                Assert.StartsWith(packagedTemp, line.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        finally { await WindowsProcessTestDirectory.DeleteAsync(root); }
    }

    [Fact]
    public async Task Cancellation_KillsTheContainer()
    {
        var root = CreateDirectory();
        try
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
            await using var process = new WindowsTaskProcessLauncher().Start(
                new TaskProcessStartRequest("powershell", root,
                    ["-NoProfile", "-Command", "Start-Sleep -Seconds 30"],
                    Isolation: Spec(root)), cancellation.Token);
            using var observed = System.Diagnostics.Process.GetProcessById(process.Id);
            _ = observed.Handle;

            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => process.WaitForExitAsync(cancellation.Token));
            await process.TerminateAsync();

            Assert.True(observed.HasExited);
        }
        finally { await WindowsProcessTestDirectory.DeleteAsync(root); }
    }

    [Fact]
    public async Task MissingGrantTarget_FailsClosedWithoutFallingBack()
    {
        var root = CreateDirectory();
        try
        {
            var missing = Path.Combine(root, "does-not-exist");
            var spec = new ProcessIsolationSpec(SandboxKey(), [missing], [], null);

            var error = Assert.Throws<InvalidOperationException>(() => new WindowsTaskProcessLauncher().Start(
                new TaskProcessStartRequest("powershell", root,
                    ["-NoProfile", "-Command", "exit 0"], Isolation: spec), Token));

            Assert.Contains("沙箱", error.Message);
        }
        finally { await WindowsProcessTestDirectory.DeleteAsync(root); }
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        string workingDirectory, string root, string command, string? sandboxKey = null)
    {
        await using var process = new WindowsTaskProcessLauncher().Start(new TaskProcessStartRequest(
            "powershell", workingDirectory, ["-NoProfile", "-Command", command],
            Isolation: Spec(root, sandboxKey)), Token);
        var output = process.StandardOutput.ReadToEndAsync(Token);
        var error = process.StandardError.ReadToEndAsync(Token);
        await process.WaitForExitAsync(Token).WaitAsync(TimeSpan.FromSeconds(30), Token);
        return (process.ExitCode, await output, await error);
    }

    private static ProcessIsolationSpec Spec(string root, string? sandboxKey = null) =>
        new(sandboxKey ?? SandboxKey(), [root], [], null);

    private static string SandboxKey() => $"mossagent-test-{Guid.NewGuid():N}"[..40];

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
