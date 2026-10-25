using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Git;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitRemotesTests
{
    [Fact]
    public async Task EmptyRepositoryListsNoRemotesButKeepsTheHeadBranchUpstreamShape()
    {
        using var fixture = new GitRemotesFixture();
        await fixture.InitializeAsync();
        var report = await fixture.ReadAsync();
        Assert.Empty(report.Remotes);
        Assert.NotNull(report.Upstream);
        Assert.False(string.IsNullOrEmpty(report.Upstream!.Branch));
        Assert.Null(report.Upstream.Remote);
        Assert.Null(report.Upstream.Merge);
    }

    [Theory]
    [InlineData("https://example.invalid/repo.git", GitRemoteCredentialMethod.Https, "https://example.invalid")]
    [InlineData("http://example.invalid:8443/repo.git", GitRemoteCredentialMethod.Https, "http://example.invalid:8443")]
    [InlineData("ssh://git@example.invalid/repo.git", GitRemoteCredentialMethod.Ssh, "ssh://example.invalid")]
    [InlineData("git://example.invalid/repo.git", GitRemoteCredentialMethod.Ssh, "git://example.invalid")]
    [InlineData("git@example.invalid:repo.git", GitRemoteCredentialMethod.Ssh, "ssh://example.invalid")]
    [InlineData("ext::custom-transport", GitRemoteCredentialMethod.Unsupported, null)]
    public async Task EndpointsAreSanitizedAndClassified(string url, GitRemoteCredentialMethod method, string? endpoint)
    {
        using var fixture = new GitRemotesFixture();
        await fixture.InitializeAsync();
        await fixture.GitAsync("remote", "add", "origin", url);
        var remote = Assert.Single((await fixture.ReadAsync()).Remotes);
        Assert.Equal("origin", remote.Name);
        Assert.Equal(method, remote.CredentialMethod);
        Assert.Equal(endpoint, remote.FetchEndpoint);
        Assert.Equal(endpoint, remote.PushEndpoint);
        Assert.False(remote.TransportsDiffer);
        Assert.Null(remote.PushUrlCount);
    }

    [Fact]
    public async Task EmbeddedCredentialsAreFlaggedWithoutEchoingUserinfo()
    {
        using var fixture = new GitRemotesFixture();
        await fixture.InitializeAsync();
        await fixture.GitAsync("remote", "add", "origin", "https://fixture-user:s3cr3t-value@example.invalid/repo.git?token=s3cr3t-value");
        var result = await fixture.ExecuteAsync(new { });
        Assert.True(result.IsSuccess, result.Summary);
        Assert.DoesNotContain("s3cr3t-value", result.ToString());
        Assert.DoesNotContain("fixture-user", result.ToString());
        Assert.Contains("\"credentialMethod\":\"embedded\"", result.Content);
        var remote = Assert.Single((await fixture.ReadAsync()).Remotes);
        Assert.Equal(GitRemoteCredentialMethod.Embedded, remote.CredentialMethod);
        Assert.Equal("https://example.invalid", remote.FetchEndpoint);
        Assert.Equal("https://example.invalid", remote.PushEndpoint);
    }

    [Fact]
    public async Task LocalAndRelativePathsReadAsLocalWithoutEchoingThePath()
    {
        using var fixture = new GitRemotesFixture();
        await fixture.InitializeAsync();
        await fixture.GitAsync("remote", "add", "origin", Path.Combine(fixture.Root, "fixture-secret-dir", "repo.git"));
        var remote = Assert.Single((await fixture.ReadAsync()).Remotes);
        Assert.Equal(GitRemoteCredentialMethod.Local, remote.CredentialMethod);
        Assert.NotNull(remote.FetchEndpoint);
        Assert.DoesNotContain("://", remote.FetchEndpoint);
        Assert.DoesNotContain("fixture-secret-dir", remote.FetchEndpoint);
        await fixture.GitAsync("remote", "set-url", "origin", "../fixture-secret-dir/repo.git");
        remote = Assert.Single((await fixture.ReadAsync()).Remotes);
        Assert.Equal(GitRemoteCredentialMethod.Local, remote.CredentialMethod);
        Assert.DoesNotContain("fixture-secret-dir", remote.FetchEndpoint!);
    }

    [Fact]
    public async Task DivergentPushUrlsExposeTheFirstEndpointAndTheCount()
    {
        using var fixture = new GitRemotesFixture();
        await fixture.InitializeAsync();
        await fixture.GitAsync("remote", "add", "origin", "https://fetch.invalid/repo.git");
        await fixture.GitAsync("config", "--add", "remote.origin.pushurl", "ssh://push.invalid/repo.git");
        await fixture.GitAsync("config", "--add", "remote.origin.pushurl", "https://push2.invalid/repo.git");
        var remote = Assert.Single((await fixture.ReadAsync()).Remotes);
        Assert.Equal("https://fetch.invalid", remote.FetchEndpoint);
        Assert.Equal("ssh://push.invalid", remote.PushEndpoint);
        Assert.Equal(2, remote.PushUrlCount);
        Assert.True(remote.TransportsDiffer);
        Assert.Equal(GitRemoteCredentialMethod.Ssh, remote.CredentialMethod);
    }

    [Fact]
    public async Task RemoteHelperVcsMarkersReadAsUnsupportedButKeepTheEndpoint()
    {
        using var fixture = new GitRemotesFixture();
        await fixture.InitializeAsync();
        await fixture.GitAsync("remote", "add", "origin", "https://example.invalid/repo.git");
        await fixture.GitAsync("config", "remote.origin.vcs", "custom-helper");
        var remote = Assert.Single((await fixture.ReadAsync()).Remotes);
        Assert.Equal(GitRemoteCredentialMethod.Unsupported, remote.CredentialMethod);
        Assert.Equal("https://example.invalid", remote.FetchEndpoint);
    }

    [Fact]
    public async Task UrlInsteadOfRewritesAreClassifiedByTheEffectiveEndpoint()
    {
        using var fixture = new GitRemotesFixture();
        await fixture.InitializeAsync();
        await fixture.GitAsync("remote", "add", "origin", "https://clean.invalid/repo.git");
        await fixture.GitAsync("config", "url.https://fixture-user:s3cr3t-value@rewritten.invalid/repo.git.insteadOf", "https://clean.invalid/repo.git");
        var result = await fixture.ExecuteAsync(new { });
        Assert.True(result.IsSuccess, result.Summary);
        Assert.DoesNotContain("s3cr3t-value", result.ToString());
        var remote = Assert.Single((await fixture.ReadAsync()).Remotes);
        Assert.Equal(GitRemoteCredentialMethod.Embedded, remote.CredentialMethod);
        Assert.Equal("https://rewritten.invalid", remote.FetchEndpoint);
    }

    [Fact]
    public async Task UpstreamFollowsTheHeadBranchAndIsNullWhenDetached()
    {
        using var fixture = new GitRemotesFixture();
        await fixture.InitializeAsync();
        var branch = (await fixture.GitAsync("symbolic-ref", "--short", "HEAD")).Trim();
        await fixture.GitAsync("config", $"branch.{branch}.remote", "origin");
        await fixture.GitAsync("config", $"branch.{branch}.merge", "refs/heads/tracked-target");
        await fixture.GitAsync("config", $"branch.{branch}.pushRemote", "backup");
        var upstream = (await fixture.ReadAsync()).Upstream;
        Assert.NotNull(upstream);
        Assert.Equal(branch, upstream!.Branch);
        Assert.Equal("origin", upstream.Remote);
        Assert.Equal("refs/heads/tracked-target", upstream.Merge);
        Assert.Equal("backup", upstream.PushRemote);
        await fixture.GitAsync("switch", "--detach", "HEAD");
        Assert.Null((await fixture.ReadAsync()).Upstream);
    }

    [Fact]
    public async Task CredentialHelperChainKeepsOrderAndMarksShellEntries()
    {
        using var fixture = new GitRemotesFixture();
        await fixture.InitializeAsync();
        await fixture.GitAsync("config", "--add", "credential.helper", "fixture-store --option");
        await fixture.GitAsync("config", "--add", "credential.helper", "!fixture-shell value");
        var helpers = (await fixture.ReadAsync()).CredentialHelpers.TakeLast(2).ToArray();
        Assert.Equal(2, helpers.Length);
        Assert.Equal(new GitCredentialHelperEntry("fixture-store --option", false), helpers[0]);
        Assert.Equal(new GitCredentialHelperEntry("!fixture-shell value", true), helpers[1]);
    }

    [Fact]
    public async Task OversizedConfigurationFailsClosed()
    {
        using var fixture = new GitRemotesFixture();
        await fixture.InitializeAsync();
        var padding = string.Concat(Enumerable.Range(0, 4096).Select(index => $"fixture-pad{index} = {new string('x', 300)}\n"));
        await File.AppendAllTextAsync(Path.Combine(fixture.Root, ".git", "config"), padding, TestContext.Current.CancellationToken);
        var result = await fixture.ExecuteAsync(new { });
        Assert.Equal("git_remotes_read_failed", result.ErrorCode);
    }

    [Fact]
    public async Task RepositoryOutsideAuthorizedRootsIsDeniedAndReadOnlyPolicyAutoApproves()
    {
        using var fixture = new GitRemotesFixture();
        await fixture.InitializeAsync();
        var denied = await fixture.ExecuteAsync(new { repository = Path.Combine(Path.GetTempPath(), "fixture-outside") },
            fixture.Context with { AuthorizedRoots = [fixture.Root] });
        Assert.Equal("path_denied", denied.ErrorCode);
        var result = await fixture.ExecuteAsync(new { }, fixture.Context with { ApprovalPolicy = ApprovalPolicy.ReadOnly });
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal(0, fixture.ApprovalRequests);
        Assert.True(fixture.Audit.Completion?.IsSuccess);
    }

    [Fact]
    public void DescriptorIsReadOnlyWithoutWriteOrNetworkCapabilities()
    {
        var tool = new GitRemotesTool(new GitRemoteStateReader(new AuthorizedPathResolver(),
            new GitIndexRepositoryProbe(), new GitCommandRunner(new FixtureTaskProcessLauncher())));
        Assert.Equal("git.remotes", tool.Descriptor.Name);
        Assert.Equal(ToolRiskLevel.ReadOnly, tool.Descriptor.RiskLevel);
        Assert.Equal(ToolCapability.FileRead | ToolCapability.Process, tool.Descriptor.RequiredCapabilities);
        Assert.False(tool.Descriptor.RequiredCapabilities.HasFlag(ToolCapability.FileWrite));
        Assert.False(tool.Descriptor.RequiredCapabilities.HasFlag(ToolCapability.Network));
        using var schema = JsonDocument.Parse(tool.Descriptor.InputSchemaJson);
        Assert.Equal("object", schema.RootElement.GetProperty("type").GetString());
        Assert.True(schema.RootElement.TryGetProperty("properties", out var properties)
            && properties.TryGetProperty("repository", out _));
    }
}
