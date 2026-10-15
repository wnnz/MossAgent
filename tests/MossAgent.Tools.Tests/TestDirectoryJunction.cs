using System.Diagnostics;
using Xunit;

namespace MossAgent.Tools.Tests;

internal static class TestDirectoryJunction
{
    public static async Task CreateAsync(string link, string target, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("cmd.exe")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(link)!
        };
        foreach (var argument in new[] { "/c", "mklink", "/J", link, target })
            startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Junction fixture failed to start.");
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        try { await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken); }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        Assert.True(process.ExitCode == 0, (await output) + (await error));
    }
}
