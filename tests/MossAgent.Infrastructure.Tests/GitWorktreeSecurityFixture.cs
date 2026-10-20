using System.Diagnostics;
using MossAgent.Domain;
using MossAgent.Infrastructure.Git;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

internal sealed class GitWorktreeSecurityFixture : IDisposable
{
    private readonly List<string> _links = [];
    private readonly GitProcessRunner _git = new(new FixtureTaskProcessLauncher());

    private GitWorktreeSecurityFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Repository = Path.Combine(Root, "repository");
        Outside = Path.Combine(Root, "outside");
        Paths = new AppDataPaths(Path.Combine(Root, "app-data"));
        Project = new ProjectProfile(Guid.NewGuid(), "fixture", Repository, [Repository], DateTimeOffset.UtcNow);
        Service = new GitWorktreeService(Paths, _git);
    }

    public string Root { get; }
    public string Repository { get; }
    public string Outside { get; }
    public AppDataPaths Paths { get; }
    public ProjectProfile Project { get; }
    public GitWorktreeService Service { get; }
    public CancellationToken Token => TestContext.Current.CancellationToken;
    public string Target(Guid taskId) => Path.Combine(Paths.WorktreesPath, taskId.ToString("N"));

    public static async Task<GitWorktreeSecurityFixture> CreateAsync()
    {
        var fixture = new GitWorktreeSecurityFixture();
        try
        {
            await fixture.InitializeAsync(fixture.Repository);
            Directory.CreateDirectory(fixture.Outside);
            await File.WriteAllTextAsync(Path.Combine(fixture.Outside, "sentinel.txt"), "untouched", fixture.Token);
            return fixture;
        }
        catch { fixture.Dispose(); throw; }
    }

    public async Task InitializeAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        await GitAsync(directory, "init", "-b", "main");
        await GitAsync(directory, "config", "user.name", "MossAgent fixture");
        await GitAsync(directory, "config", "user.email", "fixture@example.invalid");
        await GitAsync(directory, "config", "core.hooksPath", Path.Combine(Root, "empty-hooks"));
        await GitAsync(directory, "commit", "--allow-empty", "-m", "initial");
    }

    public async Task<string> GitAsync(string directory, params string[] arguments)
    {
        var result = await _git.RunAsync(directory, arguments, Token);
        Assert.True(result.ExitCode == 0, $"Git fixture failed: {result.StandardError}");
        Assert.False(result.OutputTruncated);
        return result.StandardOutput.TrimEnd('\r', '\n');
    }

    public async Task JunctionAsync(string link, string target)
    {
        Assert.StartsWith(Root + Path.DirectorySeparatorChar, Path.GetFullPath(link), StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(Root + Path.DirectorySeparatorChar, Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase);
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        if (!OperatingSystem.IsWindows()) Directory.CreateSymbolicLink(link, target);
        else
        {
            var start = new ProcessStartInfo("cmd.exe")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            foreach (var argument in new[] { "/c", "mklink", "/J", link, target }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync(Token);
            var stderr = process.StandardError.ReadToEndAsync(Token);
            await process.WaitForExitAsync(Token);
            Assert.True(process.ExitCode == 0, $"Junction fixture failed: {await stdout} {await stderr}");
        }
        _links.Add(link);
    }

    public async Task AssertSentinelAsync()
    {
        Assert.Equal("untouched", await File.ReadAllTextAsync(Path.Combine(Outside, "sentinel.txt"), Token));
    }

    public void Dispose()
    {
        foreach (var link in _links.AsEnumerable().Reverse())
            if (new DirectoryInfo(link).LinkTarget is not null) Directory.Delete(link);
        if (!Directory.Exists(Root)) return;
        foreach (var entry in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            File.SetAttributes(entry, FileAttributes.Normal);
        Directory.Delete(Root, recursive: true);
    }
}
