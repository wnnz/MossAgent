using CodingAgent.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CodingAgent.Infrastructure.Security;

/// <summary>HS256 JWT 令牌（无外部依赖），经 httpOnly Cookie 携带。</summary>
public class JwtCookieTokenService(string signingKeyBase64) : ITokenService
{
    public const string CookieName = "codingagent_auth";

    private readonly byte[] _key = Convert.FromBase64String(signingKeyBase64);

    public string IssueToken(TimeSpan lifetime)
    {
        var now = DateTimeOffset.UtcNow;
        var payload = new Dictionary<string, object>
        {
            ["sub"] = "local-user",
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.Add(lifetime).ToUnixTimeSeconds(),
        };

        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, string> { ["alg"] = "HS256", ["typ"] = "JWT" }));
        var body = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signature = Base64UrlEncode(Sign($"{header}.{body}"));
        return $"{header}.{body}.{signature}";
    }

    public bool TryValidate(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                return false;
            }

            var expected = Base64UrlEncode(Sign($"{parts[0]}.{parts[1]}"));
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(parts[2])))
            {
                return false;
            }

            var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Base64UrlDecode(parts[1]));
            if (payload is null || !payload.TryGetValue("exp", out var exp))
            {
                return false;
            }

            return exp.GetInt64() >= DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return false;
        }
    }

    private byte[] Sign(string data) => new HMACSHA256(_key).ComputeHash(Encoding.UTF8.GetBytes(data));

    private static string Base64UrlEncode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded + new string('=', (4 - padded.Length % 4) % 4));
    }
}
