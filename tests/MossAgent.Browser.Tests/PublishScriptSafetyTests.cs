using System.IO.Compression;
using Xunit;

namespace MossAgent.Browser.Tests;

public sealed class PublishScriptSafetyTests
{
    [Theory(Timeout = 30000)]
    [InlineData("restore-failure")]
    [InlineData("publish-failure")]
    [InlineData("missing-executable")]
    [InlineData("multiple-assets")]
    [InlineData("install-archive-failure")]
    public async Task FailedPublication_PreservesPreviousExecutableAndArchive(string scenario)
    {
        using var sandbox = new PublishScriptSandbox();
        sandbox.Seed();
        var result = await sandbox.RunAsync(scenario, TestContext.Current.CancellationToken);
        Assert.NotEqual(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(sandbox.Root, "calls.txt")), result.Output);
        var expected = scenario switch
        {
            "restore-failure" => "dotnet restore 失败",
            "publish-failure" => "dotnet publish 失败",
            "missing-executable" => "没有生成 MossAgent.App.exe",
            "multiple-assets" => "只包含 MossAgent.App.exe",
            _ => "Synthetic archive promotion failure"
        };
        Assert.Contains(expected, result.Output);
        AssertPreviousPackage(sandbox);
        AssertNoTemporaryArtifacts(sandbox);
    }

    [Fact(Timeout = 30000)]
    public async Task DefaultPublication_PublishesOnlyOneExecutableUsingWindowsProfile()
    {
        using var sandbox = new PublishScriptSandbox();
        sandbox.Seed();
        var result = await sandbox.RunAsync("single-file", TestContext.Current.CancellationToken);
        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Equal("new executable", File.ReadAllText(Path.Combine(sandbox.OutputDirectory, "MossAgent.App.exe")));
        Assert.Equal("MossAgent.App.exe", Path.GetFileName(Assert.Single(Directory.GetFiles(sandbox.OutputDirectory))));
        using var archive = ZipFile.OpenRead(sandbox.ArchivePath);
        Assert.Equal("MossAgent.App.exe", Assert.Single(archive.Entries).FullName);
        var calls = File.ReadAllLines(Path.Combine(sandbox.Root, "calls.txt"));
        Assert.Contains(calls, call => call.StartsWith("restore|", StringComparison.Ordinal)
            && call.Contains("-p:PublishProfile=WindowsX64", StringComparison.Ordinal));
        Assert.Contains(calls, call => call.StartsWith("publish|", StringComparison.Ordinal)
            && call.Contains("-p:PublishProfile=WindowsX64|", StringComparison.Ordinal));
        AssertNoTemporaryArtifacts(sandbox);
    }

    [Fact(Timeout = 30000)]
    public async Task ArchiveReplacementFailure_RollsBackAlreadyMovedOutput()
    {
        using var sandbox = new PublishScriptSandbox();
        sandbox.Seed();
        using (var locked = File.Open(sandbox.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var result = await sandbox.RunAsync("single-file", TestContext.Current.CancellationToken);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("publish|", File.ReadAllText(Path.Combine(sandbox.Root, "calls.txt")));
        }
        AssertPreviousPackage(sandbox);
        AssertNoTemporaryArtifacts(sandbox);
    }

    [Fact(Timeout = 30000)]
    public async Task JunctionArtifactDirectory_IsRejectedWithoutTouchingTargetOrRunningDotnet()
    {
        using var sandbox = new PublishScriptSandbox();
        var result = await sandbox.RunAsync("junction-artifacts", TestContext.Current.CancellationToken);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("重解析点", result.Output);
        Assert.Equal("untouched", File.ReadAllText(Path.Combine(sandbox.Root, "outside", "marker.txt")));
        Assert.False(File.Exists(Path.Combine(sandbox.Root, "calls.txt")));
    }

    [Theory(Timeout = 30000)]
    [InlineData("win-arm64", "0.1.0")]
    [InlineData("../escape", "0.1.0")]
    [InlineData("win-x64", "../escape")]
    public async Task UnsupportedRuntimeOrUnsafeVersion_IsRejectedBeforeRunningDotnet(string runtime, string version)
    {
        using var sandbox = new PublishScriptSandbox();
        var result = await sandbox.RunAsync("single-file", TestContext.Current.CancellationToken, runtime, version);
        Assert.NotEqual(0, result.ExitCode);
        Assert.False(File.Exists(Path.Combine(sandbox.Root, "calls.txt")));
        AssertNoTemporaryArtifacts(sandbox);
    }

    private static void AssertPreviousPackage(PublishScriptSandbox sandbox)
    {
        Assert.Equal("previous executable", File.ReadAllText(Path.Combine(sandbox.OutputDirectory, "MossAgent.App.exe")));
        Assert.Equal("previous archive", File.ReadAllText(sandbox.ArchivePath));
    }

    private static void AssertNoTemporaryArtifacts(PublishScriptSandbox sandbox) =>
        Assert.DoesNotContain(Directory.GetFileSystemEntries(sandbox.Artifacts), path =>
            Path.GetFileName(path).StartsWith(".staging-", StringComparison.Ordinal)
            || Path.GetFileName(path).StartsWith(".backup-", StringComparison.Ordinal));
}
