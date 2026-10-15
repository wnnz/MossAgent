using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProfileDisplaySafetyTests
{
    [Fact]
    public void SecretBearingProfilesUseOnlyTheirNameForDisplay()
    {
        var proxy = new ProxyProfile(Guid.NewGuid(), "Proxy", ProxyProtocol.Http, "internal.test", 8080,
            "private-user", "private-password", false, true);
        var provider = new AiProvider(Guid.NewGuid(), "Provider", ProviderProtocol.OpenAiResponses,
            new Uri("https://internal.test/v1"), "private-api-key", proxy.Id, false, true,
            new Dictionary<string, string> { ["Authorization"] = "private-header" });

        Assert.Equal("Proxy", proxy.ToString());
        Assert.Equal("Provider", provider.ToString());
        Assert.DoesNotContain("private-", proxy.ToString());
        Assert.DoesNotContain("private-", provider.ToString());
        Assert.Equal("private-password", (proxy with { Name = "Renamed" }).Password);
        Assert.Equal("Renamed", (provider with { Name = "Renamed" }).ToString());
    }

    [Fact]
    public void McpProfileDisplay_DoesNotExposeArgumentsEnvironmentHeadersOrCredentialBearingUrl()
    {
        var profile = new McpServerProfile(Guid.NewGuid(), "MCP", McpTransportKind.Stdio,
            "private-command", "[\"private-argument\"]", "private-directory",
            "{\"TOKEN\":\"private-environment\"}",
            new Uri("https://private-user:private-password@internal.test/mcp?token=private-query"),
            "{\"Authorization\":\"private-header\"}", Guid.NewGuid(), true);

        Assert.Equal("MCP", profile.ToString());
        Assert.DoesNotContain("private-", profile.ToString());
        var renamed = profile with { Name = "Renamed" };
        Assert.Equal("Renamed", renamed.ToString());
        Assert.Equal(profile.EnvironmentJson, renamed.EnvironmentJson);
        Assert.Equal(profile.HeadersJson, renamed.HeadersJson);
        Assert.Equal(profile.ArgumentsJson, renamed.ArgumentsJson);
    }
}
