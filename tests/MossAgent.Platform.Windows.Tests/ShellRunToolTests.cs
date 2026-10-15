using System.Diagnostics;
using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Processes;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Processes;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class ShellRunToolTests
{
    [Theory]
    [InlineData("[Console]::Out.Write('raw-output')", "raw-output")]
    [InlineData("[Console]::Error.Write('raw-error')", "raw-error")]
    [InlineData("[Console]::Out.Write('中文输出')", "中文输出")]
    [InlineData("[Console]::Error.Write('中文错误')", "中文错误")]
    [InlineData("Write-Output '中文输出'", "中文输出\r\n")]
    [InlineData("using namespace System; [Console]::Out.Write('namespace-ok')", "namespace-ok")]
    [InlineData("param(); [Console]::Out.Write('param-ok')", "param-ok")]
    public async Task RealProcess_PreservesRawCharactersWithoutAddingNewlines(string command, string expected)
    {
        var root = CreateRoot();
        try
        {
            var result = await CreateExecutor().ExecuteAsync(Request(command), Context(root),
                TestContext.Current.CancellationToken);
            Assert.True(result.IsSuccess, result.Summary);
            Assert.Equal(expected.Replace("\r\n", Environment.NewLine, StringComparison.Ordinal), result.Content);
        }
        finally { await WindowsProcessTestDirectory.DeleteAsync(root); }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(512)]
    public async Task LargeUnterminatedLine_IsBoundedByPipeline(int limit)
    {
        var root = CreateRoot();
        try
        {
            var result = await CreateExecutor().ExecuteAsync(
                Request("[Console]::Out.Write(('x' * 4000000))"),
                Context(root) with { MaximumOutputCharacters = limit }, TestContext.Current.CancellationToken);
            Assert.True(result.IsSuccess);
            Assert.InRange(result.OutputCharacterCount, 0, limit);
        }
        finally { await WindowsProcessTestDirectory.DeleteAsync(root); }
    }

    [Fact]
    public async Task TimedOutProcess_CannotContinueWritingAfterReturn()
    {
        var root = CreateRoot();
        try
        {
            var execution = CreateExecutor().ExecuteAsync(
                Request("[IO.File]::WriteAllText('process.pid.tmp', [string]$PID); "
                    + "[IO.File]::Move('process.pid.tmp', 'process.pid'); "
                    + "Start-Sleep -Seconds 30; [IO.File]::WriteAllText('late.txt', 'late')"),
                Context(root) with { Timeout = TimeSpan.FromSeconds(3) }, TestContext.Current.CancellationToken);
            using var process = await WaitForProcessAsync(root);
            Assert.False(process.HasExited);
            var result = await execution;
            Assert.Equal("tool_timeout", result.ErrorCode);
            Assert.True(process.HasExited);
            Assert.False(File.Exists(Path.Combine(root, "late.txt")));
        }
        finally { await WindowsProcessTestDirectory.DeleteAsync(root); }
    }

    private static async Task<Process> WaitForProcessAsync(string root)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var path = Path.Combine(root, "process.pid");
        while (!File.Exists(path)) await Task.Delay(15, timeout.Token);
        var processId = int.Parse(await File.ReadAllTextAsync(path, timeout.Token));
        return Process.GetProcessById(processId);
    }

    private static ToolExecutor CreateExecutor() => new(
        new ToolRegistry([new ShellRunTool(new AuthorizedPathResolver(), new WindowsTaskProcessLauncher())]),
        new PolicyApprovalService((_, _, _) => ValueTask.FromResult(true)));

    private static ToolRequest Request(string command) => new("shell-test", "shell.run",
        JsonSerializer.SerializeToElement(new { command }));

    private static ToolExecutionContext Context(string root) => new(
        Guid.NewGuid(), root, [root], ApprovalPolicy.FullAccess, Timeout: TimeSpan.FromSeconds(15));

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
