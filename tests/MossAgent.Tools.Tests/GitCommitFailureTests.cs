using System.Text.Json;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions.Git;
using MossAgent.Tools.Abstractions.Processes;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitCommitFailureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HookOrIdentityFailurePreservesHeadIndexAndWorkingContent(bool identity)
    {
        using var fixture = new GitIndexFixture();
        await fixture.InitializeAsync();
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "selected.txt"), "retained\n", TestContext.Current.CancellationToken);
        await fixture.GitAsync("add", "--", "selected.txt");
        var head = await fixture.GitAsync("rev-parse", "HEAD");
        if (identity)
        {
            await fixture.GitAsync("config", "user.name", "");
            await fixture.GitAsync("config", "user.email", "");
        }
        else
        {
            var hooks = Directory.CreateDirectory(Path.Combine(fixture.Root, "fixture-hooks")).FullName;
            await File.WriteAllTextAsync(Path.Combine(hooks, "pre-commit"), "#!/bin/sh\nprintf 'controlled-hook-rejection' >&2\nexit 1\n", TestContext.Current.CancellationToken);
            await fixture.GitAsync("config", "core.hooksPath", hooks);
        }
        var read = await fixture.ExecuteAsync("git.commit_preview", new { });
        Assert.True(read.IsSuccess, read.Summary);
        var preview = JsonSerializer.Deserialize<GitCommitPreview>(read.Content!, JsonSerializerOptions.Web)!;
        var result = await fixture.ExecuteAsync("git.commit", new { message = "must be retained", expectedState = preview.State });
        Assert.Equal("git_failed", result.ErrorCode);
        Assert.Equal(head, await fixture.GitAsync("rev-parse", "HEAD"));
        Assert.Equal("selected.txt\0", await fixture.GitAsync("diff", "--cached", "--name-only", "-z"));
        Assert.Equal("retained\n", await File.ReadAllTextAsync(Path.Combine(fixture.Root, "selected.txt"), TestContext.Current.CancellationToken));
        Assert.False(fixture.Audit.Completion?.IsSuccess);
    }

    [Fact]
    public async Task UnmergedIndexIsRefusedByPreviewAndCommitWithoutCreatingACommit()
    {
        using var fixture = new GitIndexFixture();
        await fixture.InitializeAsync();
        var token = TestContext.Current.CancellationToken;
        var head = await fixture.GitAsync("rev-parse", "HEAD");
        var blob = (await fixture.GitAsync("rev-parse", "HEAD:tracked.txt")).Trim();
        await using (var process = new FixtureTaskProcessLauncher().Start(new TaskProcessStartRequest("git", fixture.Root,
            ["update-index", "--index-info"], RedirectStandardInput: true, RemovedEnvironmentPrefixes: ["GIT_"]), token))
        {
            var input = $"0 {new string('0', 40)}\ttracked.txt\n100644 {blob} 1\ttracked.txt\n100644 {blob} 2\ttracked.txt\n100644 {blob} 3\ttracked.txt\n";
            await process.StandardInput!.WriteAsync(input.AsMemory(), token);
            process.StandardInput.Dispose();
            await process.WaitForExitAsync(token);
            Assert.Equal(0, process.ExitCode);
        }
        Assert.Equal("git_unmerged", (await fixture.ExecuteAsync("git.commit_preview", new { })).ErrorCode);
        Assert.Equal("git_unmerged", (await fixture.ExecuteAsync("git.commit", new { message = "not committed", expectedState = new string('0', 64) })).ErrorCode);
        Assert.Equal(head, await fixture.GitAsync("rev-parse", "HEAD"));
    }
}
