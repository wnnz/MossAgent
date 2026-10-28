using MossAgent.Platform.Windows.Processes;
using MossAgent.Tools.Abstractions.Processes;
using Xunit;

namespace MossAgent.Testing;

/// <summary>
/// Environment precondition for tests whose product path launches Git inside the
/// AppContainer sandbox: the git install directory must be package-readable or
/// DACL-grantable for the current user. When it is not, the sandbox correctly
/// fails closed (that behavior is asserted directly by SandboxTests) — so tests
/// that need a working sandboxed git skip instead of failing on the machine.
/// </summary>
internal static class SandboxedGitSupport
{
    private static readonly Lazy<Task<bool>> Available = new(ProbeAsync);

    public static async Task RequireAsync()
    {
        if (!await Available.Value.ConfigureAwait(false))
            Assert.Skip("本机 Git 安装目录不可沙箱授权（无包可读 ACL 且当前用户无权改写）。");
    }

    private static async Task<bool> ProbeAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", "sandbox-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var launcher = new WindowsTaskProcessLauncher();
            var spec = new ProcessIsolationSpec($"mossagent-probe-{Guid.NewGuid():N}"[..48], [root], [], null);
            try
            {
                await using var process = launcher.Start(
                    new TaskProcessStartRequest("git", root, ["--version"], Isolation: spec), CancellationToken.None);
                await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
                return process.ExitCode == 0;
            }
            catch (Exception) { return false; }
        }
        finally
        {
            try { Directory.Delete(root, true); }
            catch (Exception) { /* probe residue only */ }
        }
    }
}
