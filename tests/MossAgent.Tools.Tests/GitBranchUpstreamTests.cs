using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Git;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitBranchUpstreamTests
{
    [Fact]
    public async Task SetsUpstreamOnTheCurrentHeadBranch()
    {
        using var fixture = new GitRemoteWriteFixture();
        await fixture.InitializeAsync();
        var branch = await fixture.HeadBranchAsync();
        var result = await fixture.ExecuteAsync("git.branch_upstream", new { remote = "origin", mergeRef = "refs/heads/tracked" });
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal("origin", (await fixture.GitAsync("config", "--get", $"branch.{branch}.remote")).Trim());
        Assert.Equal("refs/heads/tracked", (await fixture.GitAsync("config", "--get", $"branch.{branch}.merge")).Trim());
        var report = JsonSerializer.Deserialize<GitRemotesUpstream>(result.Content!, JsonSerializerOptions.Web)!;
        Assert.Equal(branch, report.Branch);
        Assert.Equal("origin", report.Remote);
        Assert.Equal("refs/heads/tracked", report.Merge);
        Assert.Equal("refs/heads/tracked", (await fixture.ReadAsync()).Upstream!.Merge);
    }

    [Fact]
    public async Task ExplicitBranchWritesThatBranchAndMissingBranchIsRejected()
    {
        using var fixture = new GitRemoteWriteFixture();
        await fixture.InitializeAsync();
        await fixture.GitAsync("branch", "other");
        var result = await fixture.ExecuteAsync("git.branch_upstream",
            new { branch = "other", remote = "origin", mergeRef = "refs/heads/tracked" });
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal("origin", (await fixture.GitAsync("config", "--get", "branch.other.remote")).Trim());
        var missing = await fixture.ExecuteAsync("git.branch_upstream",
            new { branch = "ghost", remote = "origin", mergeRef = "refs/heads/tracked" });
        Assert.Equal("git_branch_missing", missing.ErrorCode);
    }

    [Fact]
    public async Task DetachedHeadWithoutExplicitBranchIsRejected()
    {
        using var fixture = new GitRemoteWriteFixture();
        await fixture.InitializeAsync();
        await fixture.GitAsync("switch", "--detach");
        var result = await fixture.ExecuteAsync("git.branch_upstream", new { remote = "origin", mergeRef = "refs/heads/tracked" });
        Assert.Equal("git_upstream_detached", result.ErrorCode);
    }

    [Fact]
    public async Task ExpectedValuesGuardTheCurrentUpstream()
    {
        using var fixture = new GitRemoteWriteFixture();
        await fixture.InitializeAsync();
        var branch = await fixture.HeadBranchAsync();
        await fixture.GitAsync("config", $"branch.{branch}.remote", "origin");
        var stale = await fixture.ExecuteAsync("git.branch_upstream", new
        {
            remote = "origin", mergeRef = "refs/heads/tracked", expectedRemote = "upstream"
        });
        Assert.Equal("git_upstream_changed", stale.ErrorCode);
        var absentMerge = await fixture.ExecuteAsync("git.branch_upstream", new
        {
            remote = "origin", mergeRef = "refs/heads/tracked", expectedRemote = "origin", expectedMergeRef = (string?)null
        });
        Assert.True(absentMerge.IsSuccess, absentMerge.Summary);
        var staleNull = await fixture.ExecuteAsync("git.branch_upstream", new
        {
            remote = "origin", mergeRef = "refs/heads/new", expectedMergeRef = (string?)null
        });
        Assert.Equal("git_upstream_changed", staleNull.ErrorCode);
        Assert.Equal("refs/heads/tracked", (await fixture.GitAsync("config", "--get", $"branch.{branch}.merge")).Trim());
    }

    [Fact]
    public async Task UpstreamWithoutExpectationsOverwritesExistingValues()
    {
        using var fixture = new GitRemoteWriteFixture();
        await fixture.InitializeAsync();
        var branch = await fixture.HeadBranchAsync();
        await fixture.GitAsync("config", $"branch.{branch}.remote", "origin");
        await fixture.GitAsync("config", $"branch.{branch}.merge", "refs/heads/old");
        var result = await fixture.ExecuteAsync("git.branch_upstream", new { remote = "origin", mergeRef = "refs/heads/new" });
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal("refs/heads/new", (await fixture.GitAsync("config", "--get", $"branch.{branch}.merge")).Trim());
    }

    [Fact]
    public async Task MissingRemoteAndInvalidMergeRefAreRejected()
    {
        using var fixture = new GitRemoteWriteFixture();
        await fixture.InitializeAsync();
        var missing = await fixture.ExecuteAsync("git.branch_upstream", new { remote = "ghost", mergeRef = "refs/heads/tracked" });
        Assert.Equal("git_remote_missing", missing.ErrorCode);
        Assert.Equal("invalid_arguments", (await fixture.ExecuteAsync("git.branch_upstream",
            new { remote = "origin", mergeRef = "heads/tracked" })).ErrorCode);
        Assert.Equal("invalid_arguments", (await fixture.ExecuteAsync("git.branch_upstream",
            new { remote = "origin", mergeRef = "refs/heads/bad name" })).ErrorCode);
        Assert.Equal("invalid_arguments", (await fixture.ExecuteAsync("git.branch_upstream",
            new { remote = "origin", mergeRef = "refs/tags/tracked" })).ErrorCode);
        Assert.Equal("invalid_arguments", (await fixture.ExecuteAsync("git.branch_upstream",
            new { remote = "bad name", mergeRef = "refs/heads/tracked" })).ErrorCode);
    }

    [Fact]
    public async Task PartialWriteReportsTheAppliedKeyHonestly()
    {
        using var fixture = new GitRemoteWriteFixture();
        await fixture.InitializeAsync();
        var branch = await fixture.HeadBranchAsync();
        await fixture.GitAsync("config", $"branch.{branch}.merge", "refs/heads/one");
        await fixture.GitAsync("config", "--add", $"branch.{branch}.merge", "refs/heads/two");
        var result = await fixture.ExecuteAsync("git.branch_upstream", new { remote = "origin", mergeRef = "refs/heads/tracked" });
        Assert.Equal("git_upstream_failed", result.ErrorCode);
        Assert.Contains("remote", result.Summary);
        Assert.Equal("origin", (await fixture.GitAsync("config", "--get", $"branch.{branch}.remote")).Trim());
        Assert.Equal(2, (await fixture.GitAsync("config", "--get-all", $"branch.{branch}.merge")).Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public async Task ApprovalPolicyDeniesReadOnlyAndDeclinedApprovalDoesNotWrite()
    {
        using var fixture = new GitRemoteWriteFixture();
        await fixture.InitializeAsync();
        var branch = await fixture.HeadBranchAsync();
        var args = new { remote = "origin", mergeRef = "refs/heads/tracked" };
        var denied = await fixture.ExecuteAsync("git.branch_upstream", args, fixture.Context with { ApprovalPolicy = ApprovalPolicy.ReadOnly });
        Assert.Equal("capability_denied", denied.ErrorCode);
        fixture.Approved = false;
        var declined = await fixture.ExecuteAsync("git.branch_upstream", args);
        Assert.Equal("approval_denied", declined.ErrorCode);
        var config = await fixture.GitAsync("config", "--null", "--list");
        Assert.DoesNotContain($"branch.{branch}.remote", config);
    }

    [Fact]
    public void DescriptorRequiresWriteCapabilitiesWithoutNetwork()
    {
        var tool = new GitBranchUpstreamTool(
            new GitBranchUpstreamReader(new AuthorizedPathResolver(), new GitIndexRepositoryProbe(), new GitCommandRunner(new FixtureTaskProcessLauncher())),
            new GitCommandRunner(new FixtureTaskProcessLauncher()));
        Assert.Equal("git.branch_upstream", tool.Descriptor.Name);
        Assert.Equal(ToolRiskLevel.ExternalSideEffect, tool.Descriptor.RiskLevel);
        Assert.Equal(ToolCapability.FileWrite | ToolCapability.Process, tool.Descriptor.RequiredCapabilities);
        Assert.False(tool.Descriptor.RequiredCapabilities.HasFlag(ToolCapability.Network));
        using var schema = JsonDocument.Parse(tool.Descriptor.InputSchemaJson);
        var required = schema.RootElement.GetProperty("required").EnumerateArray().Select(value => value.GetString()!).Order().ToArray();
        Assert.Equal("mergeRef", required[0]);
        Assert.Equal("remote", required[1]);
        var properties = schema.RootElement.GetProperty("properties");
        Assert.True(properties.TryGetProperty("expectedRemote", out var expected)
            && expected.GetProperty("type").EnumerateArray().Any(type => type.GetString() == "null"));
        Assert.Equal("^refs/heads/.", properties.GetProperty("mergeRef").GetProperty("pattern").GetString());
    }
}
