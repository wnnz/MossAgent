using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Processes;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Processes;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Processes;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

/// <summary>
/// Boundary matrix for the AppContainer sandbox: reparse-point escapes, ripgrep
/// through the migrated tool path and multiple authorized roots. Real container
/// launches like <see cref="SandboxTests"/>; the ripgrep case skips when rg is
/// not resolvable on PATH.
/// </summary>
public sealed class SandboxBoundaryTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task JunctionEscapeToOutsideDirectory_IsDenied()
    {
        var root = CreateDirectory();
        var outside = CreateDirectory();
        var junction = Path.Combine(root, "link");
        try
        {
            var secret = Path.Combine(outside, "secret.txt");
            await File.WriteAllTextAsync(secret, "must-not-leak", Token);
            using var link = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo(
                    "cmd.exe", $"/c mklink /J \"{junction}\" \"{outside}\"")
                { CreateNoWindow = true, UseShellExecute = false }
            };
            link.Start();
            await link.WaitForExitAsync(Token);
            Assert.Equal(0, link.ExitCode);

            await using var process = new WindowsTaskProcessLauncher().Start(
                new TaskProcessStartRequest("powershell", root,
                    ["-NoProfile", "-Command", $"[IO.File]::ReadAllText('{Path.Combine(junction, "secret.txt")}')"],
                    Isolation: Spec(root)), Token);
            var output = process.StandardOutput.ReadToEndAsync(Token);
            var error = process.StandardError.ReadToEndAsync(Token);
            await process.WaitForExitAsync(Token).WaitAsync(TimeSpan.FromSeconds(30), Token);

            Assert.NotEqual(0, process.ExitCode);
            Assert.DoesNotContain("must-not-leak", await output);
        }
        finally
        {
            // Directory.Delete(recursive:true) cannot unlink a junction.
            if (Directory.Exists(junction)) Directory.Delete(junction, recursive: false);
            await WindowsProcessTestDirectory.DeleteAsync(root);
            await WindowsProcessTestDirectory.DeleteAsync(outside);
        }
    }

    [Fact]
    public async Task SearchTextTool_RunsRipgrepInsideTheContainer()
    {
        await RequireSandboxedRipgrepAsync();
        var root = CreateDirectory();
        var outside = CreateDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "hit.txt"), "needle-in-root", Token);
            await File.WriteAllTextAsync(Path.Combine(outside, "secret.txt"), "needle-outside", Token);
            var context = new ToolExecutionContext(Guid.NewGuid(), root, [root], ApprovalPolicy.FullAccess);
            var tool = new SearchTextTool(new AuthorizedPathResolver(), new WindowsTaskProcessLauncher());

            var inside = await tool.ExecuteAsync(
                new ToolRequest("call-1", "filesystem.search_text",
                    JsonSerializer.SerializeToElement(new { query = "needle" })),
                context, Token);
            Assert.True(inside.IsSuccess, inside.Summary);
            Assert.Contains("hit.txt", inside.Content);
            Assert.DoesNotContain("secret.txt", inside.Content);

            await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() => tool.ExecuteAsync(
                new ToolRequest("call-2", "filesystem.search_text",
                    JsonSerializer.SerializeToElement(new { query = "needle", path = outside })),
                context, Token));

            // Pointing the sandboxed rg directly at an outside path is denied
            // at the DACL even though the process itself is fine.
            await using var denied = new WindowsTaskProcessLauncher().Start(
                new TaskProcessStartRequest("rg", root,
                    ["--line-number", "--color=never", "--", "needle-outside", outside],
                    Isolation: Spec(root)), Token);
            var output = denied.StandardOutput.ReadToEndAsync(Token);
            var error = denied.StandardError.ReadToEndAsync(Token);
            await denied.WaitForExitAsync(Token).WaitAsync(TimeSpan.FromSeconds(30), Token);

            Assert.NotEqual(0, denied.ExitCode);
            Assert.DoesNotContain("needle-outside", await output);
        }
        finally
        {
            await WindowsProcessTestDirectory.DeleteAsync(root);
            await WindowsProcessTestDirectory.DeleteAsync(outside);
        }
    }

    [Fact]
    public async Task MultipleAuthorizedRoots_AreAllReachable()
    {
        var first = CreateDirectory();
        var second = CreateDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(first, "a.txt"), "first-root", Token);
            await File.WriteAllTextAsync(Path.Combine(second, "b.txt"), "second-root", Token);
            var spec = new ProcessIsolationSpec(SandboxKey(), [first, second], [], null);

            await using var process = new WindowsTaskProcessLauncher().Start(
                new TaskProcessStartRequest("powershell", first,
                    ["-NoProfile", "-Command",
                        $"[Console]::Out.WriteLine([IO.File]::ReadAllText('{Path.Combine(first, "a.txt")}'));"
                        + $"[Console]::Out.WriteLine([IO.File]::ReadAllText('{Path.Combine(second, "b.txt")}'))"],
                    Isolation: spec), Token);
            var output = process.StandardOutput.ReadToEndAsync(Token);
            await process.WaitForExitAsync(Token).WaitAsync(TimeSpan.FromSeconds(30), Token);

            var text = await output;
            Assert.Equal(0, process.ExitCode);
            Assert.Contains("first-root", text);
            Assert.Contains("second-root", text);
        }
        finally
        {
            await WindowsProcessTestDirectory.DeleteAsync(first);
            await WindowsProcessTestDirectory.DeleteAsync(second);
        }
    }

    private static async Task RequireSandboxedRipgrepAsync()
    {
        var root = CreateDirectory();
        try
        {
            using var launcher = new WindowsTaskProcessLauncher();
            var spec = new ProcessIsolationSpec(SandboxKey(), [root], [], null);
            try
            {
                await using var process = launcher.Start(
                    new TaskProcessStartRequest("rg", root, ["--version"], Isolation: spec), Token);
                await process.WaitForExitAsync(Token).WaitAsync(TimeSpan.FromSeconds(30), Token);
                if (process.ExitCode != 0)
                    Assert.Skip("rg resolved but did not run inside the AppContainer on this machine.");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                Assert.Skip("rg is unavailable or its install directory is not sandbox-grantable here.");
            }
        }
        finally
        {
            await WindowsProcessTestDirectory.DeleteAsync(root);
        }
    }

    private static ProcessIsolationSpec Spec(string root) =>
        new(SandboxKey(), [root], [], null);

    private static string SandboxKey() => $"mossagent-test-{Guid.NewGuid():N}"[..40];

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
