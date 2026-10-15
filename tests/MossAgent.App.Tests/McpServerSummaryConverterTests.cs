using System.Globalization;
using MossAgent.App.Controls;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class McpServerSummaryConverterTests
{
    [Fact]
    public void HttpSummary_ExcludesEmbeddedCredentialsAndQuery()
    {
        var profile = new McpServerProfile(Guid.NewGuid(), "server", McpTransportKind.StreamableHttp,
            null, "[]", null, "{}",
            new Uri("https://user:private-password@mcp.example.test:8443/mcp?token=private-query"),
            "{}", null, true);

        var summary = new McpServerSummaryConverter().Convert(profile, typeof(string), null,
            CultureInfo.InvariantCulture);

        Assert.Equal("Streamable HTTP · https://mcp.example.test:8443", summary);
        Assert.DoesNotContain("private-password", Assert.IsType<string>(summary));
        Assert.DoesNotContain("private-query", Assert.IsType<string>(summary));
    }

    [Fact]
    public void StdioSummary_UsesReadableTransportNameWithoutCommand()
    {
        var profile = new McpServerProfile(Guid.NewGuid(), "server", McpTransportKind.Stdio,
            "command-with-secret", "[]", null, "{}", null, "{}", null, false);

        var summary = new McpServerSummaryConverter().Convert(profile, typeof(string), null,
            CultureInfo.InvariantCulture);

        Assert.Equal("标准输入输出 (stdio)", summary);
        Assert.DoesNotContain("secret", Assert.IsType<string>(summary));
    }
}
