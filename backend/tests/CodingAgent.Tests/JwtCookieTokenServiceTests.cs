using System.Text.Json;
using CodingAgent.Infrastructure.Security;
using Xunit;

namespace CodingAgent.Tests;

public class JwtCookieTokenServiceTests
{
    private static JwtCookieTokenService CreateService(byte? seed = null) =>
        new(Convert.ToBase64String(Enumerable.Range(0, 64).Select(i => (byte)(i * (seed ?? 1) % 255 + 1)).ToArray()));

    [Fact]
    public void IssueToken_ThenValidate_Passes()
    {
        var service = CreateService();
        var token = service.IssueToken(TimeSpan.FromMinutes(5));
        Assert.True(service.TryValidate(token));
    }

    [Fact]
    public void Validate_ExpiredToken_ReturnsFalse()
    {
        var service = CreateService();
        var token = service.IssueToken(TimeSpan.FromSeconds(-10));
        Assert.False(service.TryValidate(token));
    }

    [Fact]
    public void Validate_TamperedPayload_ReturnsFalse()
    {
        var service = CreateService();
        var token = service.IssueToken(TimeSpan.FromMinutes(5));
        var parts = token.Split('.');
        // 篡改 payload（exp 改大）但不重签
        var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            Base64UrlDecode(parts[1]))!;
        payload["exp"] = JsonSerializer.SerializeToElement(DateTimeOffset.UtcNow.AddYears(1).ToUnixTimeSeconds());
        var tampered = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(payload))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.False(service.TryValidate($"{parts[0]}.{tampered}.{parts[2]}"));
    }

    [Fact]
    public void Validate_TamperedSignature_ReturnsFalse()
    {
        var service = CreateService(1);
        var token = service.IssueToken(TimeSpan.FromMinutes(5));
        var parts = token.Split('.');
        var other = CreateService(2);
        // 错误密钥重签
        var forged = other.IssueToken(TimeSpan.FromMinutes(5)).Split('.');
        Assert.False(service.TryValidate($"{parts[0]}.{parts[1]}.{forged[2]}"));
    }

    [Fact]
    public void Validate_BadFormat_ReturnsFalse()
    {
        var service = CreateService();
        Assert.False(service.TryValidate("not-a-jwt"));
        Assert.False(service.TryValidate(""));
        Assert.False(service.TryValidate("a.b.c.d"));
    }

    private static string Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        return System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(padded + new string('=', (4 - padded.Length % 4) % 4)));
    }
}
