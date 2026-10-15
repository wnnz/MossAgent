using System.Diagnostics;
using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Processes;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class AuthorizedPathResolverTests
{
    [Fact]
    public void Resolve_RejectsPathOutsideAuthorizedRoots()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var context = new ToolExecutionContext(
            Guid.NewGuid(), root, [root], ApprovalPolicy.AskEveryTime);
        var resolver = new AuthorizedPathResolver();
        var outside = Path.GetFullPath(Path.Combine(root, "..", "outside.txt"));

        Assert.Throws<UnauthorizedAccessException>(() => resolver.Resolve(outside, context));
    }

    [Fact]
    public void Resolve_AllowsPathInsideAuthorizedRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var context = new ToolExecutionContext(
            Guid.NewGuid(), root, [root], ApprovalPolicy.AskEveryTime);
        var resolver = new AuthorizedPathResolver();
        var expected = Path.Combine(root, "src", "file.cs");

        Assert.Equal(Path.GetFullPath(expected), resolver.Resolve(expected, context));
    }

    [Fact]
    public void Resolve_DoesNotTreatWorkingDirectoryAsAnAuthorizedRoot()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        var authorized = Path.Combine(basePath, "authorized");
        var outside = Path.Combine(basePath, "outside");
        var context = new ToolExecutionContext(
            Guid.NewGuid(), outside, [authorized], ApprovalPolicy.FullAccess);
        var resolver = new AuthorizedPathResolver();

        Assert.Throws<UnauthorizedAccessException>(() => resolver.Resolve("secret.txt", context));
        Assert.Throws<UnauthorizedAccessException>(() => resolver.Resolve(outside, context));
        Assert.Throws<UnauthorizedAccessException>(() => resolver.Resolve(outside,
            context with { AuthorizedRoots = [] }));
    }

    [Fact]
    public void Resolve_AllowsRelativePathInSecondaryAuthorizedRoot()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        var primary = Path.Combine(basePath, "primary");
        var secondary = Path.Combine(basePath, "secondary");
        var context = new ToolExecutionContext(
            Guid.NewGuid(), secondary, [primary, secondary], ApprovalPolicy.FullAccess);

        var resolved = new AuthorizedPathResolver().Resolve("src\\file.cs", context);

        Assert.Equal(Path.GetFullPath(Path.Combine(secondary, "src", "file.cs")), resolved);
    }

    [Fact]
    public async Task ShellRun_RejectsUnlistedWorkingDirectoryBeforeStartingProcess()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        var authorized = Directory.CreateDirectory(Path.Combine(basePath, "authorized")).FullName;
        var outside = Directory.CreateDirectory(Path.Combine(basePath, "outside")).FullName;
        var marker = Path.Combine(outside, "shell-executed.txt");
        try
        {
            var tool = new ShellRunTool(new AuthorizedPathResolver(), new RejectingTaskProcessLauncher());
            var request = new ToolRequest("call-shell", tool.Descriptor.Name,
                JsonSerializer.SerializeToElement(new
                {
                    command = $"Set-Content -Path '{marker}' -Value escaped"
                }));
            var context = new ToolExecutionContext(
                Guid.NewGuid(), outside, [authorized], ApprovalPolicy.FullAccess);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tool.ExecuteAsync(
                request, context, TestContext.Current.CancellationToken));
            Assert.False(File.Exists(marker));
        }
        finally
        {
            Directory.Delete(basePath, recursive: true);
        }
    }

    [Fact]
    public void Resolve_RejectsLinkThatEscapesAuthorizedRoot()
    {
        var testRoot = Path.Combine(
            Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        var authorized = Directory.CreateDirectory(Path.Combine(testRoot, "authorized")).FullName;
        var outside = Directory.CreateDirectory(Path.Combine(testRoot, "outside")).FullName;
        var link = Path.Combine(authorized, "escape");
        try
        {
            CreateJunction(link, outside);
            var context = new ToolExecutionContext(
                Guid.NewGuid(), authorized, [authorized], ApprovalPolicy.AskEveryTime);

            var action = () => new AuthorizedPathResolver().Resolve(
                Path.Combine(link, "secret.txt"), context);

            Assert.Throws<UnauthorizedAccessException>(action);
        }
        finally
        {
            if (Directory.Exists(link))
            {
                Directory.Delete(link);
            }

            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ShellRun_RejectsWorkingDirectoryJunctionEscape()
    {
        var testRoot = Path.Combine(
            Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        var authorized = Directory.CreateDirectory(Path.Combine(testRoot, "authorized")).FullName;
        var outside = Directory.CreateDirectory(Path.Combine(testRoot, "outside")).FullName;
        var link = Path.Combine(authorized, "escape");
        var marker = Path.Combine(outside, "shell-executed.txt");
        try
        {
            CreateJunction(link, outside);
            var tool = new ShellRunTool(new AuthorizedPathResolver(), new RejectingTaskProcessLauncher());
            var request = new ToolRequest(
                "call-shell", tool.Descriptor.Name,
                JsonSerializer.SerializeToElement(new
                {
                    command = $"Set-Content -Path '{marker}' -Value escaped",
                    workingDirectory = link
                }));
            var context = new ToolExecutionContext(
                Guid.NewGuid(), authorized, [authorized], ApprovalPolicy.FullAccess);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tool.ExecuteAsync(
                request, context, TestContext.Current.CancellationToken));

            Assert.False(File.Exists(marker));
        }
        finally
        {
            if (Directory.Exists(link))
            {
                Directory.Delete(link);
            }

            Directory.Delete(testRoot, recursive: true);
        }
    }

    private static void CreateJunction(string link, string target)
    {
        var startInfo = new ProcessStartInfo("cmd.exe")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("mklink");
        startInfo.ArgumentList.Add("/J");
        startInfo.ArgumentList.Add(link);
        startInfo.ArgumentList.Add(target);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动 junction fixture 命令。");
        process.WaitForExit();
        Assert.True(
            process.ExitCode == 0,
            $"创建 junction 失败：{process.StandardError.ReadToEnd()}");
    }
}
