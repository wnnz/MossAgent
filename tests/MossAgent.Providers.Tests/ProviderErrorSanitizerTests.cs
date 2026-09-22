using MossAgent.Domain;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class ProviderErrorSanitizerTests
{
    [Fact]
    public void Sanitize_RedactsApiKeyAndCustomHeaderValues()
    {
        var provider = new AiProvider(
            Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
            new Uri("https://example.com/v1/"),
            "api-secret-value", null, true, true,
            new Dictionary<string, string>
            {
                ["Authorization"] = "custom-secret-value",
                ["X-Empty"] = string.Empty
            });
        var error = "first\r\napi-secret-value and custom-secret-value";

        var result = ProviderErrorSanitizer.Sanitize(error, provider);

        Assert.DoesNotContain("api-secret-value", result, StringComparison.Ordinal);
        Assert.DoesNotContain("custom-secret-value", result, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", result, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", result, StringComparison.Ordinal);
        Assert.Contains("***", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Sanitize_BoundsUntrustedResponseBody()
    {
        var provider = new AiProvider(
            Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
            new Uri("https://example.com/v1/"), "secret", null,
            true, true, new Dictionary<string, string>());

        var result = ProviderErrorSanitizer.Sanitize(new string('x', 2000), provider);

        Assert.Equal(501, result.Length);
        Assert.EndsWith("…", result, StringComparison.Ordinal);
    }
}
