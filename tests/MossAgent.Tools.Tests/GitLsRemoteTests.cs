using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Git;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitLsRemoteTests
{
    [Fact]
    public async Task LocalBareRemoteReturnsAllHeadsAndTagsWithoutPrompting()
    {
        using var fixture = new GitLsRemoteFixture();
        await fixture.InitializeAsync();
        var head = (await fixture.GitAsync("rev-parse", "HEAD")).Trim();
        var report = await fixture.ProbeAsync(new { remote = "origin" });
        Assert.Equal("本地 Git 仓库", report.Endpoint);
        Assert.False(report.Truncated);
        Assert.Equal(head, Assert.Single(report.Refs, entry => entry.Ref == "refs/heads/main").Oid);
        Assert.Equal(head, Assert.Single(report.Refs, entry => entry.Ref == "refs/heads/topic").Oid);
        Assert.Single(report.Refs, entry => entry.Ref == "refs/tags/v1");
        Assert.All(report.Refs, entry => Assert.StartsWith("refs/", entry.Ref, StringComparison.Ordinal));
        Assert.Equal(0, fixture.ApprovalRequests);
    }

    [Fact]
    public async Task RefFilterNormalizesShortNamesAndFullRefs()
    {
        using var fixture = new GitLsRemoteFixture();
        await fixture.InitializeAsync();
        var main = await fixture.ProbeAsync(new { remote = "origin", @ref = "main" });
        Assert.Equal("refs/heads/main", Assert.Single(main.Refs).Ref);
        var nested = await fixture.ProbeAsync(new { remote = "origin", @ref = "refs/heads/topic" });
        Assert.Equal("refs/heads/topic", Assert.Single(nested.Refs).Ref);
        var tag = await fixture.ProbeAsync(new { remote = "origin", @ref = "refs/tags/v1" });
        Assert.Single(tag.Refs, entry => entry.Ref == "refs/tags/v1");
        Assert.All(tag.Refs, entry => Assert.StartsWith("refs/tags/v1", entry.Ref, StringComparison.Ordinal));
        var head = await fixture.ProbeAsync(new { remote = "origin", @ref = "HEAD" });
        Assert.Empty(head.Refs);
    }

    [Fact]
    public async Task MissingRemoteFailsWithFixedCodeBeforeProbing()
    {
        using var fixture = new GitLsRemoteFixture();
        await fixture.InitializeAsync();
        var result = await fixture.ExecuteAsync(new { remote = "ghost" });
        Assert.Equal("git_remote_missing", result.ErrorCode);
        Assert.DoesNotContain(fixture.Launcher.Requests, request => request.Arguments.Contains("ls-remote"));
    }

    [Fact]
    public async Task UnsupportedFetchTransportIsRejectedBeforeProbing()
    {
        using var fixture = new GitLsRemoteFixture();
        await fixture.InitializeAsync();
        await fixture.GitAsync("remote", "set-url", "origin", "ftp://example.invalid/repo");
        var result = await fixture.ExecuteAsync(new { remote = "origin" });
        Assert.Equal("git_remote_probe_transport", result.ErrorCode);
        Assert.DoesNotContain(fixture.Launcher.Requests, request => request.Arguments.Contains("ls-remote"));
    }

    [Fact]
    public async Task VcsHelperMarkerIsRejectedBeforeProbing()
    {
        using var fixture = new GitLsRemoteFixture();
        await fixture.InitializeAsync();
        await fixture.GitAsync("config", "remote.origin.vcs", "custom");
        var result = await fixture.ExecuteAsync(new { remote = "origin" });
        Assert.Equal("git_remote_probe_transport", result.ErrorCode);
        Assert.DoesNotContain(fixture.Launcher.Requests, request => request.Arguments.Contains("ls-remote"));
    }

    [Fact]
    public async Task LocalTargetOutsideAuthorizedRootsIsDenied()
    {
        using var fixture = new GitLsRemoteFixture();
        await fixture.InitializeAsync();
        await fixture.GitAsync("remote", "set-url", "origin", fixture.OtherRemote);
        var denied = await fixture.ExecuteAsync(new { remote = "origin" },
            fixture.Context with { AuthorizedRoots = [fixture.Root] });
        Assert.Equal("path_denied", denied.ErrorCode);
        Assert.DoesNotContain(fixture.Launcher.Requests, request => request.Arguments.Contains("ls-remote"));
    }

    [Theory]
    [InlineData("bad name")]
    [InlineData("refs/heads/bad name")]
    [InlineData("refs/../x")]
    [InlineData("refs/")]
    [InlineData("-x")]
    [InlineData("@")]
    [InlineData("release@{0}")]
    [InlineData("refs/heads/a\u0001")]
    public async Task InvalidRefShapesAreRejectedDuringValidation(string value)
    {
        using var fixture = new GitLsRemoteFixture();
        await fixture.InitializeAsync();
        var result = await fixture.ExecuteAsync(new { remote = "origin", @ref = value });
        Assert.Equal("invalid_arguments", result.ErrorCode);
        Assert.DoesNotContain(fixture.Launcher.Requests, request => request.Arguments.Contains("ls-remote"));
    }

    [Fact]
    public async Task RemoteNonZeroExitFailsClosedWithoutEchoingRawOutput()
    {
        using var fixture = new GitLsRemoteFixture();
        await fixture.InitializeAsync();
        await fixture.GitAsync("config", "remote.origin.uploadpack", "definitely-missing-probe-pack");
        var result = await fixture.ExecuteAsync(new { remote = "origin" });
        Assert.Equal("git_remote_probe_failed", result.ErrorCode);
        Assert.DoesNotContain("definitely-missing-probe-pack", result.ToString());
        Assert.DoesNotContain(fixture.Remote, result.ToString());
        Assert.Null(result.Content);
    }

    [Fact]
    public async Task RemoteRefusalFailsClosedWithoutEchoingUrlOrCredentials()
    {
        using var fixture = new GitLsRemoteFixture();
        await fixture.InitializeAsync();
        await fixture.GitAsync("remote", "set-url", "origin", "https://fixture-user:s3cr3t-value@127.0.0.1:1/repo.git");
        var result = await fixture.ExecuteAsync(new { remote = "origin" });
        Assert.Equal("git_remote_probe_failed", result.ErrorCode);
        Assert.DoesNotContain("s3cr3t-value", result.ToString());
        Assert.DoesNotContain("127.0.0.1", result.ToString());
    }

    [Fact]
    public async Task ReadOnlyPolicyAutoAllowsTheReadOnlyProbe()
    {
        using var fixture = new GitLsRemoteFixture();
        await fixture.InitializeAsync();
        var result = await fixture.ExecuteAsync(new { remote = "origin" },
            fixture.Context with { ApprovalPolicy = ApprovalPolicy.ReadOnly });
        Assert.True(result.IsSuccess, result.Summary);
        var report = JsonSerializer.Deserialize<GitLsRemoteReport>(result.Content!, JsonSerializerOptions.Web)!;
        Assert.Equal("origin", report.Remote);
    }

    [Fact]
    public async Task ProbeArgumentsDisableOtherProtocolsPromptsAndUseIsolatedEnvironment()
    {
        using var fixture = new GitLsRemoteFixture();
        await fixture.InitializeAsync();
        await fixture.ProbeAsync(new { remote = "origin" });
        var request = Assert.Single(fixture.Launcher.Requests, entry => entry.Arguments.Contains("ls-remote"));
        var arguments = request.Arguments.ToArray();
        Assert.Equal(new[] { "--", "origin" }, arguments.TakeLast(2));
        Assert.Contains("ls-remote", arguments);
        Assert.Contains("--heads", arguments);
        Assert.Contains("--tags", arguments);
        Assert.Contains("protocol.allow=never", arguments);
        Assert.Contains("protocol.file.allow=always", arguments);
        Assert.Contains("core.askPass=", arguments);
        Assert.DoesNotContain(arguments, value => value.Contains("sshCommand"));
        Assert.Equal("0", request.Environment?["GIT_TERMINAL_PROMPT"]);
        Assert.Equal("never", request.Environment?["GCM_INTERACTIVE"]);
    }

    [Fact]
    public async Task MoreThanSixtyFourRefsAreReportedTruncated()
    {
        using var fixture = new GitLsRemoteFixture();
        await fixture.InitializeAsync();
        await fixture.PushAsync(70);
        var report = await fixture.ProbeAsync(new { remote = "origin" });
        Assert.True(report.Truncated);
        Assert.Equal(64, report.Refs.Count);
        Assert.All(report.Refs, entry => Assert.StartsWith("refs/", entry.Ref, StringComparison.Ordinal));
    }

    [Fact]
    public void DescriptorDeclaresReadOnlyRiskAndReadCapabilities()
    {
        var tool = new GitLsRemoteTool(
            new GitRemoteUrlReader(new AuthorizedPathResolver(), new GitIndexRepositoryProbe(), new GitCommandRunner(new FixtureTaskProcessLauncher())),
            new GitRemoteUrlTarget(new GitPushDestinationProbe()), new GitCommandRunner(new FixtureTaskProcessLauncher()));
        Assert.Equal("git.ls_remote", tool.Descriptor.Name);
        Assert.Equal(ToolRiskLevel.ReadOnly, tool.Descriptor.RiskLevel);
        Assert.Equal(ToolCapability.FileRead | ToolCapability.Process | ToolCapability.Network, tool.Descriptor.RequiredCapabilities);
        Assert.True(ToolCapabilityPolicy.IsAllowed(tool.Descriptor, ApprovalPolicy.ReadOnly));
        using var schema = JsonDocument.Parse(tool.Descriptor.InputSchemaJson);
        Assert.Equal("remote", Assert.Single(schema.RootElement.GetProperty("required").EnumerateArray()).GetString());
        var properties = schema.RootElement.GetProperty("properties");
        Assert.True(properties.TryGetProperty("ref", out var filter) && filter.GetProperty("maxLength").GetInt32() == 1024);
    }
}
