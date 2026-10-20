using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Git;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitCommitWorkflowTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task OrdinaryCommitUsesOnlyStagedBytesWithInitialAndDetachedHeadSupport(bool committed, bool detached)
    {
        using var fixture = new GitIndexFixture();
        await fixture.InitializeAsync(committed);
        if (detached) await fixture.GitAsync("checkout", "--detach");
        var token = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "selected.txt"), "staged bytes\n", token);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "unselected.txt"), "not committed\n", token);
        Assert.True((await fixture.ExecuteAsync("git.stage_file", new { path = "selected.txt" })).IsSuccess);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "selected.txt"), "later working bytes\n", token);
        var preview = await PreviewAsync(fixture);
        Assert.Equal(1, preview.StagedFiles);
        Assert.Equal(committed, preview.Head is not null);
        Assert.Equal(detached, preview.Branch is null);
        var result = await fixture.ExecuteAsync("git.commit", new { message = "中文提交说明\n\n保留正文与 --all 字面内容。", expectedState = preview.State });
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal("staged bytes\n", await fixture.GitAsync("show", "HEAD:selected.txt"));
        Assert.Contains("selected.txt", await fixture.GitAsync("ls-tree", "--name-only", "HEAD"));
        Assert.DoesNotContain("unselected.txt", await fixture.GitAsync("ls-tree", "--name-only", "HEAD"));
        Assert.Equal("later working bytes\n", await File.ReadAllTextAsync(Path.Combine(fixture.Root, "selected.txt"), token));
        Assert.Equal("not committed\n", await File.ReadAllTextAsync(Path.Combine(fixture.Root, "unselected.txt"), token));
        Assert.Contains("保留正文与 --all", await fixture.GitAsync("log", "-1", "--format=%B"));
        Assert.True(fixture.Audit.Completion?.IsSuccess);
    }

    [Theory]
    [InlineData("index")]
    [InlineData("branch")]
    [InlineData("head")]
    public async Task ApprovalTimeStateChangesRefuseCommitWithoutReplayingOrOverwriting(string change)
    {
        using var fixture = new GitIndexFixture();
        await fixture.InitializeAsync();
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "selected.txt"), "selected\n", TestContext.Current.CancellationToken);
        await fixture.GitAsync("add", "--", "selected.txt");
        var preview = await PreviewAsync(fixture);
        string? expectedHead = null;
        fixture.OnApproval = async () =>
        {
            if (change == "index") await fixture.GitAsync("reset", "--quiet", "--", "selected.txt");
            else if (change == "branch") await fixture.GitAsync("checkout", "-b", "another-branch");
            else await fixture.GitAsync("-c", "core.hooksPath=" + Path.Combine(fixture.Root, "disabled-test-hooks"), "commit", "--allow-empty", "-m", "external commit");
            expectedHead = await fixture.GitAsync("rev-parse", "HEAD");
        };
        var result = await fixture.ExecuteAsync("git.commit", new { message = "must not run", expectedState = preview.State });
        Assert.Equal("git_state_changed", result.ErrorCode);
        Assert.Equal(expectedHead, await fixture.GitAsync("rev-parse", "HEAD"));
        Assert.DoesNotContain("must not run", await fixture.GitAsync("log", "--format=%B"));
    }

    [Fact]
    public async Task EmptyStagingDenialReadOnlyAndInvalidMessagesNeverCommit()
    {
        using var fixture = new GitIndexFixture();
        await fixture.InitializeAsync();
        var head = await fixture.GitAsync("rev-parse", "HEAD");
        var preview = await PreviewAsync(fixture);
        Assert.Equal(0, preview.StagedFiles);
        var arguments = new { message = "not committed", expectedState = preview.State };
        Assert.Equal("git_nothing_staged", (await fixture.ExecuteAsync("git.commit", arguments)).ErrorCode);
        fixture.Approved = false;
        Assert.Equal("approval_denied", (await fixture.ExecuteAsync("git.commit", arguments)).ErrorCode);
        Assert.Equal("capability_denied", (await fixture.ExecuteAsync("git.commit", arguments, fixture.Context with { ApprovalPolicy = ApprovalPolicy.ReadOnly })).ErrorCode);
        fixture.Approved = true;
        foreach (var message in new[] { " ", "invalid\0message", "invalid\u001bmessage" })
            Assert.Equal("invalid_arguments", (await fixture.ExecuteAsync("git.commit", new { message, expectedState = preview.State })).ErrorCode);
        Assert.Equal("invalid_arguments", (await fixture.ExecuteAsync("git.commit", new { message = "valid", expectedState = "fake", all = true })).ErrorCode);
        Assert.Equal(head, await fixture.GitAsync("rev-parse", "HEAD"));
    }

    private static async Task<GitCommitPreview> PreviewAsync(GitIndexFixture fixture)
    {
        var result = await fixture.ExecuteAsync("git.commit_preview", new { });
        Assert.True(result.IsSuccess, result.Summary);
        return JsonSerializer.Deserialize<GitCommitPreview>(result.Content!, JsonSerializerOptions.Web)!;
    }
}
