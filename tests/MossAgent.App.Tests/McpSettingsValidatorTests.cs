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

    [Fact]
    public void TryValidate_RejectsUrlCredentials()
    {
        var valid = McpSettingsValidator.TryValidate(
            "server", McpTransportKind.StreamableHttp, string.Empty,
            "https://user:private-password@example.test/mcp", "[]", "{}", "{}", out _, out var error);

        Assert.False(valid);
        Assert.Contains("不能包含账号或密码", error);
        Assert.DoesNotContain("private-password", error);
    }

    [Theory]
    [InlineData("[42]", "{}", "{}")]
    [InlineData("[]", "{\"KEY\":null}", "{}")]
    [InlineData("[]", "{}", "{\"Authorization\":42}")]
    public void TryValidate_RequiresStringValues(string arguments, string environment, string headers)
    {
        var valid = McpSettingsValidator.TryValidate(
            "server", McpTransportKind.Stdio, "pwsh", string.Empty,
            arguments, environment, headers, out _, out var error);

        Assert.False(valid);
        Assert.Contains("所有值均须为字符串", error);
    }

    [Fact]
    public void TryValidate_MalformedJsonDoesNotEchoSecrets()
    {
        const string secret = "private-bearer-token";
        var valid = McpSettingsValidator.TryValidate(
            "server", McpTransportKind.Stdio, "pwsh", string.Empty,
            "[]", "{}", "{\"Authorization\":\"private-bearer-token\"",
            out _, out var error);

        Assert.False(valid);
        Assert.Contains("JSON 配置无效", error);
        Assert.DoesNotContain(secret, error);
    }
}
