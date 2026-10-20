using System.Text.Json;
using MossAgent.Platform.Windows.Processes;
using MossAgent.Tools.Abstractions.Processes;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class WindowsProcessInputEnvironmentTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RedirectedInputAndEnvironment_PreserveUnicodeWithoutChangingHostEnvironment()
    {
        var variable = "MOSSAGENT_PROCESS_FIXTURE_" + Guid.NewGuid().ToString("N");
        var script = "[Console]::InputEncoding = [Text.UTF8Encoding]::new($false); "
            + "[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false); "
            + "$message = [Console]::In.ReadLine(); [Console]::Out.WriteLine((ConvertTo-Json -Compress -InputObject "
            + $"@([Environment]::GetEnvironmentVariable('{variable}'), $message)))";
        var request = new TaskProcessStartRequest("pwsh", Path.GetTempPath(), ["-NoProfile", "-Command", script],
            RedirectStandardInput: true, Environment: new Dictionary<string, string?> { [variable] = "环境 🚀 with spaces" });
        await using var process = new WindowsTaskProcessLauncher().Start(request, Token);

        await Assert.IsAssignableFrom<TextWriter>(process.StandardInput).WriteLineAsync("输入中文 and quotes \"".AsMemory(), Token);
        var output = await process.StandardOutput.ReadToEndAsync(Token).WaitAsync(TimeSpan.FromSeconds(10), Token);
        await process.WaitForExitAsync(Token);

        Assert.Equal(0, process.ExitCode);
        Assert.Equal(["环境 🚀 with spaces", "输入中文 and quotes \""],
            Assert.IsType<string[]>(JsonSerializer.Deserialize<string[]>(output)));
        Assert.Null(Environment.GetEnvironmentVariable(variable));
    }

    [Fact]
    public async Task NullEnvironmentOverride_RemovesInheritedValueOnlyInTheChild()
    {
        var hostValue = Environment.GetEnvironmentVariable("ComSpec");
        Assert.False(string.IsNullOrEmpty(hostValue));
        var request = new TaskProcessStartRequest("pwsh", Path.GetTempPath(),
            ["-NoProfile", "-Command", "[Console]::Out.Write([Environment]::GetEnvironmentVariable('ComSpec'))"],
            Environment: new Dictionary<string, string?> { ["cOmSpEc"] = null });
        await using var process = new WindowsTaskProcessLauncher().Start(request, Token);

        var output = await process.StandardOutput.ReadToEndAsync(Token).WaitAsync(TimeSpan.FromSeconds(10), Token);
        await process.WaitForExitAsync(Token);

        Assert.Equal(string.Empty, output);
        Assert.Equal(hostValue, Environment.GetEnvironmentVariable("ComSpec"));
    }

    [Fact]
    public async Task DefaultInput_IsClosedAndDoesNotBlockACommandWaitingForEof()
    {
        var request = new TaskProcessStartRequest("pwsh", Path.GetTempPath(),
            ["-NoProfile", "-Command", "[Console]::Out.Write([Console]::In.ReadToEnd().Length)"]);
        await using var process = new WindowsTaskProcessLauncher().Start(request, Token);

        Assert.Null(process.StandardInput);
        Assert.Equal("0", await process.StandardOutput.ReadToEndAsync(Token).WaitAsync(TimeSpan.FromSeconds(10), Token));
        await process.WaitForExitAsync(Token);
    }

    [Theory]
    [InlineData("", "fixture-secret")]
    [InlineData("NAME=INJECTED", "fixture-secret")]
    [InlineData("NAME\0INJECTED", "fixture-secret")]
    [InlineData("NAME", "fixture-secret\0INJECTED")]
    public void InvalidEnvironment_FailsBeforeLaunchWithoutEchoingValues(string name, string value)
    {
        var request = new TaskProcessStartRequest("pwsh", Path.GetTempPath(), ["-NoProfile", "-Command", "exit 0"],
            Environment: new Dictionary<string, string?> { [name] = value });

        var error = Assert.Throws<ArgumentException>(() => new WindowsTaskProcessLauncher().Start(request, Token));

        Assert.DoesNotContain("fixture-secret", error.ToString());
    }

    [Fact]
    public async Task RemovedPrefixesExcludeInheritedAndOverrideVariablesOnlyInTheChild()
    {
        var host = Environment.GetEnvironmentVariable("ComSpec");
        var request = new TaskProcessStartRequest("pwsh", Path.GetTempPath(), ["-NoProfile", "-Command",
            "[Console]::Write((ConvertTo-Json -Compress @([Environment]::GetEnvironmentVariable('GIT_INDEX_FILE'), [Environment]::GetEnvironmentVariable('GIT_TRACE'), [Environment]::GetEnvironmentVariable('ComSpec'), $env:MOSSAGENT_RETAINED_FIXTURE)))"],
            Environment: new Dictionary<string, string?> { ["GIT_INDEX_FILE"] = "untrusted-index", ["GIT_TRACE"] = "untrusted-log", ["MOSSAGENT_RETAINED_FIXTURE"] = "retained" },
            RemovedEnvironmentPrefixes: ["git_", "comspec"]);
        await using var process = new WindowsTaskProcessLauncher().Start(request, Token);
        var output = await process.StandardOutput.ReadToEndAsync(Token).WaitAsync(TimeSpan.FromSeconds(10), Token);
        await process.WaitForExitAsync(Token);
        Assert.Equal(0, process.ExitCode);
        Assert.Equal(new string?[] { null, null, null, "retained" }, JsonSerializer.Deserialize<string?[]>(output));
        Assert.Equal(host, Environment.GetEnvironmentVariable("ComSpec"));
        Assert.Null(Environment.GetEnvironmentVariable("MOSSAGENT_RETAINED_FIXTURE"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("NAME=VALUE")]
    [InlineData("NAME\0")]
    public void InvalidRemovalPrefixIsRejectedBeforeLaunch(string prefix)
    {
        var request = new TaskProcessStartRequest("pwsh", Path.GetTempPath(), ["-NoProfile", "-Command", "exit 0"], RemovedEnvironmentPrefixes: [prefix]);
        Assert.Throws<ArgumentException>(() => new WindowsTaskProcessLauncher().Start(request, Token));
    }
}
