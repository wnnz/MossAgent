using MossAgent.Application.Models;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sanitize_PreservesNonFailureEventIdentity(bool completed)
    {
        var provider = ProviderStreamingTestFixture.CreateRequest(ProviderProtocol.OpenAiResponses).Provider;
        ModelEvent modelEvent = completed ? new ModelCompleted() : new TextDelta("hello");

        var result = ProviderErrorSanitizer.Sanitize(modelEvent, provider);

        Assert.Same(modelEvent, result);
    }

    [Fact]
    public void Sanitize_RedactsFailureThroughBaseEventTypeWithoutMutatingIt()
    {
        var provider = ProviderStreamingTestFixture.CreateRequest(ProviderProtocol.OpenAiResponses,
            headers: new Dictionary<string, string> { ["X-Secret"] = "fixture-header-secret" }).Provider;
        ModelEvent modelEvent = new ModelFailed(provider.ApiKey, $"{provider.Headers["X-Secret"]}\r\n");

        var result = ProviderErrorSanitizer.Sanitize(modelEvent, provider);

        Assert.True(result is ModelFailed);
        var failure = (ModelFailed)result;
        Assert.Equal("***", failure.Code);
        Assert.Equal("***  ", failure.Message);
        Assert.True(((ModelFailed)modelEvent).Code == provider.ApiKey);
        Assert.NotSame(modelEvent, result);
    }
}
