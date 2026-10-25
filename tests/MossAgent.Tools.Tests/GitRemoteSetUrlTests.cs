using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Git;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitRemoteSetUrlTests
{
    private static object Expect(string? endpoint, string method) => new { endpoint, method };

    [Fact]
    public async Task SetUrlUpdatesFetchUrlAfterMatchingSemanticExpectations()
    {
        using var fixture = new GitRemoteWriteFixture();
        await fixture.InitializeAsync();
        var remote = await fixture.RemoteEntryAsync();
        var result = await fixture.ExecuteAsync("git.remote_set_url", new
        {
            remote = "origin", url = "https://new.invalid/repo.git",
            expectedFetch = Expect(remote.FetchEndpoint, "https"), expectedPush = Expect(remote.PushEndpoint, "https")
        });
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal("https://new.invalid/repo.git", (await fixture.GitAsync("remote", "get-url", "origin")).Trim());
        var updated = await fixture.RemoteEntryAsync();
        Assert.Equal("https://new.invalid", updated.FetchEndpoint);
        Assert.Equal(GitRemoteCredentialMethod.Https, updated.CredentialMethod);
        var report = JsonSerializer.Deserialize<GitRemoteEntry>(result.Content!, JsonSerializerOptions.Web)!;
        Assert.Equal("https://new.invalid", report.FetchEndpoint);
    }

    [Fact]
    public async Task PushUrlCanBeCreatedOnlyWhenNoIndependentPushUrlExists()
    {
        using var fixture = new GitRemoteWriteFixture();
        await fixture.InitializeAsync();
        var result = await fixture.ExecuteAsync("git.remote_set_url", new
        {
            remote = "origin", pushUrl = "ssh://push.invalid/repo.git", expectedPush = Expect(null, "local")
        });
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal("https://old.invalid/repo.git", (await fixture.GitAsync("remote", "get-url", "origin")).Trim());
        Assert.Equal("ssh://push.invalid/repo.git", (await fixture.GitAsync("remote", "get-url", "--push", "origin")).Trim());
        var remote = await fixture.RemoteEntryAsync();
        Assert.Equal("https://old.invalid", remote.FetchEndpoint);
        Assert.Equal("ssh://push.invalid", remote.PushEndpoint);
        Assert.True(remote.TransportsDiffer);
        var stale = await fixture.ExecuteAsync("git.remote_set_url", new
        {
            remote = "origin", pushUrl = "ssh://other.invalid/repo.git", expectedPush = Expect(null, "local")
        });
        Assert.Equal("git_remote_changed", stale.ErrorCode);
        Assert.Equal("ssh://push.invalid/repo.git", (await fixture.GitAsync("remote", "get-url", "--push", "origin")).Trim());
    }

    [Fact]
    public async Task NullFetchEndpointCreatesTheMissingUrl()
    {
        using var fixture = new GitRemoteWriteFixture();
        await fixture.InitializeAsync();
        await fixture.GitAsync("config", "remote.origin.pushurl", "https://push.invalid/repo.git");
        await fixture.GitAsync("config", "--unset-all", "remote.origin.url");
        var result = await fixture.ExecuteAsync("git.remote_set_url", new
        {
            remote = "origin", url = "https://new.invalid/repo.git", expectedFetch = Expect(null, "local")
        });
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal("https://new.invalid/repo.git", (await fixture.GitAsync("remote", "get-url", "origin")).Trim());
        Assert.Equal("https://push.invalid/repo.git", (await fixture.GitAsync("remote", "get-url", "--push", "origin")).Trim());
        var stale = await fixture.ExecuteAsync("git.remote_set_url", new
        {
            remote = "origin", url = "https://third.invalid/repo.git", expectedFetch = Expect(null, "local")
        });
        Assert.Equal("git_remote_changed", stale.ErrorCode);
    }

    [Fact]
    public async Task BothLegsUpdateSequentiallyWhenBothExpectationsMatch()
    {
        using var fixture = new GitRemoteWriteFixture();
        await fixture.InitializeAsync();
        await fixture.GitAsync("remote", "set-url", "--push", "origin", "ssh://push.invalid/repo.git");
        var result = await fixture.ExecuteAsync("git.remote_set_url", new
        {
            remote = "origin", url = "https://fetch2.invalid/repo.git", pushUrl = "https://push2.invalid/repo.git",
            expectedFetch = Expect("https://old.invalid", "https"), expectedPush = Expect("ssh://push.invalid", "ssh")
        });
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal("https://fetch2.invalid/repo.git", (await fixture.GitAsync("remote", "get-url", "origin")).Trim());
        Assert.Equal("https://push2.invalid/repo.git", (await fixture.GitAsync("remote", "get-url", "--push", "origin")).Trim());
    }

    [Fact]
    public async Task StaleEndpointOrMethodExpectationsRefuseWithoutWriting()
    {
        using var fixture = new GitRemoteWriteFixture();
        await fixture.InitializeAsync();
        var stale = await fixture.ExecuteAsync("git.remote_set_url", new
        {
            remote = "origin", url = "https://new.invalid/repo.git", expectedFetch = Expect("https://stale.invalid", "https")
        });
        Assert.Equal("git_remote_changed", stale.ErrorCode);
        var method = await fixture.ExecuteAsync("git.remote_set_url", new
        {
            remote = "origin", url = "https://new.invalid/repo.git", expectedFetch = Expect("https://old.invalid", "ssh")
        });
        Assert.Equal("git_remote_changed", method.ErrorCode);
        var push = await fixture.ExecuteAsync("git.remote_set_url", new
        {
            remote = "origin", url = "https://new.invalid/repo.git",
            expectedFetch = Expect("https://old.invalid", "https"), expectedPush = Expect("ssh://push.invalid", "ssh")
        });
        Assert.Equal("git_remote_changed", push.ErrorCode);
        Assert.Equal("https://old.invalid/repo.git", (await fixture.GitAsync("remote", "get-url", "origin")).Trim());
    }

    [Theory]
    [InlineData("ext::custom-transport")]
    [InlineData("vcs::helper https://example.invalid/repo.git")]
    [InlineData("custom://example.invalid/repo")]
    [InlineData("ftp://example.invalid/repo")]
    public async Task UnsupportedTransportsAreRejectedBeforeWriting(string url)
    {
        using var fixture = new GitRemoteWriteFixture();
        await fixture.InitializeAsync();
        var result = await fixture.ExecuteAsync("git.remote_set_url", new
        {
            remote = "origin", url, expectedFetch = Expect("https://old.invalid", "https")
        });
        Assert.Equal("git_remote_transport", result.ErrorCode);
        Assert.Equal("https://old.invalid/repo.git", (await fixture.GitAsync("remote", "get-url", "origin")).Trim());
    }

    [Fact]
    public async Task LocalTargetInsideAuthorizedRootsSucceedsAndOutsideRootsIsDenied()
    {
        using var fixture = new GitRemoteWriteFixture();
        await fixture.InitializeAsync();
        var local = await fixture.ExecuteAsync("git.remote_set_url", new
        {
            remote = "origin", url = fixture.Remote, expectedFetch = Expect("https://old.invalid", "https")
        });
        Assert.True(local.IsSuccess, local.Summary);
        var remote = await fixture.RemoteEntryAsync();
        Assert.Equal(GitRemoteCredentialMethod.Local, remote.CredentialMethod);
        Assert.DoesNotContain(fixture.Remote, remote.FetchEndpoint!);
        var denied = await fixture.ExecuteAsync("git.remote_set_url", new
        {
            remote = "origin", url = fixture.OtherRemote, expectedFetch = Expect(remote.FetchEndpoint, "local")
        }, fixture.Context with { AuthorizedRoots = [fixture.Root] });
        Assert.Equal("path_denied", denied.ErrorCode);
        Assert.Equal(fixture.Remote, (await fixture.GitAsync("remote", "get-url", "origin")).Trim());
    }

    [Fact]
    public async Task EmbeddedCredentialsAreAllowedButNeverEchoed()
    {
        using var fixture = new GitRemoteWriteFixture();
        await fixture.InitializeAsync();
        var result = await fixture.ExecuteAsync("git.remote_set_url", new
        {
            remote = "origin", url = "https://fixture-user:s3cr3t-value@example.invalid/repo.git",
            expectedFetch = Expect("https://old.invalid", "https")
        });
        Assert.True(result.IsSuccess, result.Summary);
        Assert.DoesNotContain("s3cr3t-value", result.ToString());
        var remote = await fixture.RemoteEntryAsync();
        Assert.Equal(GitRemoteCredentialMethod.Embedded, remote.CredentialMethod);
        Assert.Equal("https://example.invalid", remote.FetchEndpoint);
    }

    [Fact]
    public async Task MissingRemoteAndUnreadableStateFailBeforeWriting()
    {
        using var fixture = new GitRemoteWriteFixture();
        await fixture.InitializeAsync();
        var result = await fixture.ExecuteAsync("git.remote_set_url", new
        {
            remote = "ghost", url = "https://new.invalid/repo.git", expectedFetch = Expect("https://old.invalid", "https")
        });
        Assert.Equal("git_remote_missing", result.ErrorCode);
    }

    [Fact]
    public async Task MultiValueUrlFailsTheWriteWithoutPartialApplication()
    {
        using var fixture = new GitRemoteWriteFixture();
        await fixture.InitializeAsync();
        await fixture.GitAsync("config", "--add", "remote.origin.url", "https://second.invalid/repo.git");
        var result = await fixture.ExecuteAsync("git.remote_set_url", new
        {
            remote = "origin", url = "https://new.invalid/repo.git", expectedFetch = Expect("https://old.invalid", "https")
        });
        Assert.Equal("git_remote_set_failed", result.ErrorCode);
        Assert.DoesNotContain("new.invalid", await fixture.GitAsync("config", "--get-all", "remote.origin.url"));
    }

    [Fact]
    public async Task PartialFailureReportsTheAppliedLegHonestly()
    {
        using var fixture = new GitRemoteWriteFixture();
        await fixture.InitializeAsync();
        await fixture.GitAsync("config", "--add", "remote.origin.pushurl", "https://one.invalid/repo.git");
        await fixture.GitAsync("config", "--add", "remote.origin.pushurl", "https://two.invalid/repo.git");
        var result = await fixture.ExecuteAsync("git.remote_set_url", new
        {
            remote = "origin", url = "https://new.invalid/repo.git", pushUrl = "https://push.invalid/repo.git",
            expectedFetch = Expect("https://old.invalid", "https"), expectedPush = Expect("https://one.invalid", "https")
        });
        Assert.Equal("git_remote_set_failed", result.ErrorCode);
        Assert.Contains("fetch", result.Summary);
        Assert.Equal("https://new.invalid/repo.git", (await fixture.GitAsync("remote", "get-url", "origin")).Trim());
        Assert.Equal("https://one.invalid/repo.git", (await fixture.GitAsync("config", "--get-all", "remote.origin.pushurl")).Split('\n')[0].Trim());
    }

    [Fact]
    public async Task WriteSideWithoutMatchingExpectationAndMissingArgumentsAreRejected()
    {
        using var fixture = new GitRemoteWriteFixture();
        await fixture.InitializeAsync();
        Assert.Equal("invalid_arguments", (await fixture.ExecuteAsync("git.remote_set_url",
            new { remote = "origin", url = "https://new.invalid/repo.git", expectedPush = Expect("https://old.invalid", "https") })).ErrorCode);
        Assert.Equal("invalid_arguments", (await fixture.ExecuteAsync("git.remote_set_url",
            new { remote = "origin", url = "https://new.invalid/repo.git" })).ErrorCode);
        Assert.Equal("invalid_arguments", (await fixture.ExecuteAsync("git.remote_set_url",
            new { remote = "origin", expectedFetch = Expect("https://old.invalid", "https") })).ErrorCode);
        Assert.Equal("invalid_arguments", (await fixture.ExecuteAsync("git.remote_set_url",
            new { remote = "origin", url = "https://new.invalid/repo.git", expectedFetch = Expect("https://old.invalid", "bogus") })).ErrorCode);
        Assert.Equal("invalid_arguments", (await fixture.ExecuteAsync("git.remote_set_url",
            new { remote = "origin", url = "https://new.invalid/repo.git", expectedFetch = Expect("https://old.invalid", "https"), extra = 1 })).ErrorCode);
        Assert.Equal("invalid_arguments", (await fixture.ExecuteAsync("git.remote_set_url",
            new { remote = "bad name", url = "https://new.invalid/repo.git", expectedFetch = Expect("https://old.invalid", "https") })).ErrorCode);
        Assert.Equal("invalid_arguments", (await fixture.ExecuteAsync("git.remote_set_url",
            new { remote = "origin", url = "https://new.invalid/repo.git\u0001", expectedFetch = Expect("https://old.invalid", "https") })).ErrorCode);
    }

    [Fact]
    public async Task ApprovalPolicyDeniesReadOnlyAndDeclinedApprovalDoesNotWrite()
    {
        using var fixture = new GitRemoteWriteFixture();
        await fixture.InitializeAsync();
        var args = new { remote = "origin", url = "https://new.invalid/repo.git", expectedFetch = Expect("https://old.invalid", "https") };
        var denied = await fixture.ExecuteAsync("git.remote_set_url", args, fixture.Context with { ApprovalPolicy = ApprovalPolicy.ReadOnly });
        Assert.Equal("capability_denied", denied.ErrorCode);
        fixture.Approved = false;
        var declined = await fixture.ExecuteAsync("git.remote_set_url", args);
        Assert.Equal("approval_denied", declined.ErrorCode);
        Assert.Equal("https://old.invalid/repo.git", (await fixture.GitAsync("remote", "get-url", "origin")).Trim());
    }

    [Fact]
    public async Task RemoteMutatedDuringApprovalIsCaughtByTheSemanticCompare()
    {
        using var fixture = new GitRemoteWriteFixture();
        await fixture.InitializeAsync();
        fixture.OnApproval = async () => await fixture.GitAsync("remote", "set-url", "origin", "https://raced.invalid/repo.git");
        var result = await fixture.ExecuteAsync("git.remote_set_url", new
        {
            remote = "origin", url = "https://new.invalid/repo.git", expectedFetch = Expect("https://old.invalid", "https")
        });
        Assert.Equal("git_remote_changed", result.ErrorCode);
        Assert.Equal("https://raced.invalid/repo.git", (await fixture.GitAsync("remote", "get-url", "origin")).Trim());
    }

    [Fact]
    public void DescriptorRequiresWriteCapabilitiesWithoutNetwork()
    {
        var tool = new GitRemoteSetUrlTool(
            new GitRemoteUrlReader(new AuthorizedPathResolver(), new GitIndexRepositoryProbe(), new GitCommandRunner(new FixtureTaskProcessLauncher())),
            new GitRemoteUrlTarget(new GitPushDestinationProbe()), new GitCommandRunner(new FixtureTaskProcessLauncher()));
        Assert.Equal("git.remote_set_url", tool.Descriptor.Name);
        Assert.Equal(ToolRiskLevel.ExternalSideEffect, tool.Descriptor.RiskLevel);
        Assert.Equal(ToolCapability.FileWrite | ToolCapability.Process, tool.Descriptor.RequiredCapabilities);
        Assert.False(tool.Descriptor.RequiredCapabilities.HasFlag(ToolCapability.Network));
        using var schema = JsonDocument.Parse(tool.Descriptor.InputSchemaJson);
        var required = schema.RootElement.GetProperty("required").EnumerateArray().Select(value => value.GetString()!).ToArray();
        Assert.Equal("remote", Assert.Single(required));
        Assert.True(schema.RootElement.TryGetProperty("allOf", out var alternatives) && alternatives.GetArrayLength() == 2);
        Assert.True(schema.RootElement.GetProperty("properties").TryGetProperty("expectedFetch", out var expected)
            && expected.GetProperty("properties").GetProperty("method").GetProperty("enum").GetArrayLength() == 5);
    }
}
