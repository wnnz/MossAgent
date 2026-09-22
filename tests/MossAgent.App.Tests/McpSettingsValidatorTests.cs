using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class McpSettingsValidatorTests
{
    [Fact]
    public void TryValidate_RequiresExpectedJsonShapes()
    {
        var valid = McpSettingsValidator.TryValidate(
            "server", McpTransportKind.Stdio, "pwsh", string.Empty,
            "{}", "{}", "{}", out _, out var error);

        Assert.False(valid);
        Assert.Contains("参数必须是数组", error);
    }

    [Theory]
    [InlineData("file:///tmp/mcp")]
    [InlineData("not-a-url")]
    public void TryValidate_HttpTransportRequiresHttpEndpoint(string url)
    {
        var valid = McpSettingsValidator.TryValidate(
            "server", McpTransportKind.StreamableHttp, string.Empty, url,
            "[]", "{}", "{}", out _, out var error);

        Assert.False(valid);
        Assert.Contains("HTTP(S)", error);
    }
}
