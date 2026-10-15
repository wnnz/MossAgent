using System.Diagnostics;
using System.ComponentModel;
using System.IO.Pipes;
using System.Text.Json;
using MossAgent.Platform.Windows.Processes;
using MossAgent.Tools.Abstractions.Processes;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class WindowsTaskProcessLauncherTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task NativeArguments_PreserveQuotesEmptyValuesUnicodeAndTrailingBackslashes()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"), "中文 with spaces");
        Directory.CreateDirectory(root);
        try
        {
            var script = Path.Combine(root, "arguments.ps1");
            await File.WriteAllTextAsync(script,
                "[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false); "
                    + "[Console]::Out.Write((ConvertTo-Json -Compress -InputObject $args))", Token);
            string[] arguments = ["", "two words", "中文", "quote\"inside", "trailing\\", "space ending\\",
                "\\\\network\\path with spaces\\", "single'quote", "semicolon;", "`literal$()"];
            await using var process = new WindowsTaskProcessLauncher().Start(
                new TaskProcessStartRequest("pwsh", root, ["-NoProfile", "-File", script, .. arguments]), Token);
            var output = process.StandardOutput.ReadToEndAsync(Token);
            await process.WaitForExitAsync(Token);

            Assert.Equal(0, process.ExitCode);
            Assert.Equal(arguments, JsonSerializer.Deserialize<string[]>(await output));
        }
        finally { await WindowsProcessTestDirectory.DeleteAsync(root); }
    }

    [Fact]
    public async Task ConcurrentJobs_StoppingOneDoesNotStopTheOther()
    {
        await using var first = StartSleepingProcess();
        await using var second = StartSleepingProcess();
        using var observedFirst = Process.GetProcessById(first.Id);
        using var observedSecond = Process.GetProcessById(second.Id);
        _ = observedFirst.Handle;
        _ = observedSecond.Handle;
        Assert.Equal("ready", await first.StandardOutput.ReadLineAsync(Token));
        Assert.Equal("ready", await second.StandardOutput.ReadLineAsync(Token));

        await first.DisposeAsync();

        Assert.True(observedFirst.HasExited);
        Assert.False(observedSecond.HasExited);
        await second.DisposeAsync();
        Assert.True(observedSecond.HasExited);
    }

    [Fact]
    public async Task Launch_DoesNotInheritUnrelatedPipeHandles()
    {
        using var unrelated = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        await using var process = StartSleepingProcess();
        unrelated.DisposeLocalCopyOfClientHandle();

        var count = await unrelated.ReadAsync(new byte[1], Token).AsTask().WaitAsync(TimeSpan.FromSeconds(5), Token);

        Assert.Equal(0, count);
        Assert.Equal("ready", await process.StandardOutput.ReadLineAsync(Token));
        using var observed = Process.GetProcessById(process.Id);
        Assert.False(observed.HasExited);
    }

    [Fact]
    public void CancelledLaunch_DoesNotCreateAProcess()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        cancellation.Cancel();
        var request = new TaskProcessStartRequest("pwsh", Path.GetTempPath(), ["-NoProfile", "-Command", "exit 0"]);

        Assert.Throws<OperationCanceledException>(() => new WindowsTaskProcessLauncher().Start(request, cancellation.Token));
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    public void Launch_RequiresAnExplicitAbsoluteWorkingDirectory(string directory)
    {
        var request = new TaskProcessStartRequest("pwsh", directory, ["-NoProfile", "-Command", "exit 0"]);

        Assert.Throws<ArgumentException>(() => new WindowsTaskProcessLauncher().Start(request, Token));
    }

    [Fact]
    public void NullCharactersInArguments_AreRejectedRatherThanTruncated()
    {
        var request = new TaskProcessStartRequest("pwsh", Path.GetTempPath(), ["-Command", "exit 0\0Write-Output wrong"]);

        Assert.Throws<ArgumentException>(() => new WindowsTaskProcessLauncher().Start(request, Token));
    }

    [Fact]
    public void NullCharactersInWorkingDirectory_AreRejectedRatherThanTruncated()
    {
        var request = new TaskProcessStartRequest("pwsh", Path.GetTempPath() + "\0outside", ["-Command", "exit 0"]);

        Assert.Throws<ArgumentException>(() => new WindowsTaskProcessLauncher().Start(request, Token));
    }

    [Fact]
    public void MissingExecutable_FailsClosedInsteadOfStartingAnUnownedFallback()
    {
        var request = new TaskProcessStartRequest(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe"),
            Path.GetTempPath(), []);

        var error = Assert.Throws<Win32Exception>(() => new WindowsTaskProcessLauncher().Start(request, Token));

        Assert.Equal(2, error.NativeErrorCode);
    }

    private static ITaskProcess StartSleepingProcess() => new WindowsTaskProcessLauncher().Start(
        new TaskProcessStartRequest("pwsh", Path.GetTempPath(),
            ["-NoProfile", "-Command", "[Console]::Out.WriteLine('ready'); Start-Sleep -Seconds 30"]), Token);
}
