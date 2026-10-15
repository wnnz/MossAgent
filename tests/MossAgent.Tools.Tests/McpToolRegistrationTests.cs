using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class McpToolRegistrationTests
{
    [Theory]
    [InlineData("same service", "same/service")]
    [InlineData("same", "same")]
    [InlineData("Service", "service")]
    [InlineData("服务 A", "服务/A")]
    [InlineData("!!!", "???")]
    public async Task CollidingServerLabels_KeepBothEndpointsAndRiskLevels(string firstName, string secondName)
    {
        await using var first = new McpHttpFixtureServer { ResultPrefix = "first" };
        await using var second = new McpHttpFixtureServer { ResultPrefix = "second", MutatingToolNames = new HashSet<string> { "echo" } };
        await using var fixture = new McpToolRegistrationFixture();
        fixture.AddProfile(firstName, first.Endpoint);
        fixture.AddProfile(secondName, second.Endpoint);
        await fixture.Manager.ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Empty(fixture.Manager.Errors);
        Assert.Equal(2, fixture.Descriptors.Count);
        var readOnly = Assert.Single(fixture.Descriptors, tool => tool.RiskLevel == ToolRiskLevel.ReadOnly);
        var mutating = Assert.Single(fixture.Descriptors, tool => tool.RiskLevel == ToolRiskLevel.ExternalSideEffect);
        Assert.Equal("first:sample", (await fixture.ExecuteAsync(readOnly.Name, ApprovalPolicy.ReadOnly)).Content);
        Assert.False((await fixture.ExecuteAsync(mutating.Name, ApprovalPolicy.ReadOnly)).IsSuccess);
        Assert.Empty(second.Calls);
        Assert.Equal("second:sample", (await fixture.ExecuteAsync(mutating.Name)).Content);
        Assert.Equal("echo", Assert.Single(first.Calls));
        Assert.Equal("echo", Assert.Single(second.Calls));
    }

    [Fact]
    public async Task CollidingRemoteNames_PreserveExactCaseAndPunctuation()
    {
        string[] names = ["read.file", "read/file", "Read_File", "read_file", "工具 A", "工具/A"];
        await using var server = new McpHttpFixtureServer { ToolNames = names };
        await using var fixture = new McpToolRegistrationFixture();
        fixture.AddProfile("tools", server.Endpoint);
        await fixture.Manager.ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Empty(fixture.Manager.Errors);
        Assert.Equal(names.Length, fixture.Descriptors.Count);
        foreach (var descriptor in fixture.Descriptors) Assert.True((await fixture.ExecuteAsync(descriptor.Name)).IsSuccess);
        Assert.Equal(names.Order(StringComparer.Ordinal), server.Calls.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ReloadOrderAndRemoval_DoNotRebindARegisteredIdentity()
    {
        await using var first = new McpHttpFixtureServer { ResultPrefix = "first" };
        await using var second = new McpHttpFixtureServer { ResultPrefix = "second" };
        await using var fixture = new McpToolRegistrationFixture();
        var profile = fixture.AddProfile("shared", first.Endpoint);
        fixture.AddProfile("shared", second.Endpoint);
        await fixture.Manager.ReloadAsync(TestContext.Current.CancellationToken);
        var names = fixture.Descriptors.Select(tool => tool.Name).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(2, names.Length);
        var routes = new Dictionary<string, string?>();
        foreach (var name in names) routes.Add(name, (await fixture.ExecuteAsync(name)).Content);
        fixture.Configurations.Profiles.Reverse();
        await fixture.Manager.ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(names, fixture.Descriptors.Select(tool => tool.Name).Order(StringComparer.Ordinal));
        foreach (var name in names) Assert.Equal(routes[name], (await fixture.ExecuteAsync(name)).Content);
        fixture.Configurations.Profiles.Remove(profile);
        await fixture.Manager.ReloadAsync(TestContext.Current.CancellationToken);
        var retiredName = Assert.Single(routes, route => route.Value == "first:sample").Key;
        Assert.False((await fixture.ExecuteAsync(retiredName)).IsSuccess);
        var retained = Assert.Single(fixture.Descriptors);
        Assert.Equal("second:sample", (await fixture.ExecuteAsync(retained.Name)).Content);
    }

    [Fact]
    public async Task DuplicateDefinitions_RejectTheWholeServerWithoutOrphanedAdapters()
    {
        await using var server = new McpHttpFixtureServer { ToolNames = ["valid", "duplicate", "duplicate"] };
        await using var fixture = new McpToolRegistrationFixture();
        fixture.AddProfile("duplicate fixture", server.Endpoint);
        await fixture.Manager.ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Empty(fixture.Descriptors);
        Assert.Single(fixture.Manager.Errors);
        Assert.True(server.DeleteObserved);
        Assert.Empty(server.Calls);
        Assert.Equal("approval.probe", Assert.Single(fixture.Tools.Descriptors).Name);
    }
}
