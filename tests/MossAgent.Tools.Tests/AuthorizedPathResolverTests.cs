using System.Diagnostics;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Files;
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
