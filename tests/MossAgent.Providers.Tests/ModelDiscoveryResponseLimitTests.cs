using System.Net;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class ModelDiscoveryResponseLimitTests
{
    private const string FixtureApiKey = "fixture-api-key";
    private const int MaximumResponseBytes = 2 * 1024 * 1024;

    [Fact]
    public async Task DiscoverAsync_AcceptsResponseAtByteLimit()
    {
        var responseBody = "{\"data\":[]}".PadRight(MaximumResponseBytes, ' ');
        using var handler = new RecordingResponseHandler(HttpStatusCode.OK, responseBody);
        var service = new ModelDiscoveryService(new StaticProviderHttpClientFactory(handler));

        var models = await service.DiscoverAsync(CreateProvider(), TestContext.Current.CancellationToken);

        Assert.Empty(models);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task DiscoverAsync_RejectsOversizedResponseWithoutExposingSecrets(HttpStatusCode statusCode)
    {
        var responseBody = $"{{\"error\":\"{FixtureApiKey}\"}}".PadRight(MaximumResponseBytes + 1, ' ');
        using var handler = new RecordingResponseHandler(statusCode, responseBody);
        var service = new ModelDiscoveryService(new StaticProviderHttpClientFactory(handler));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.DiscoverAsync(CreateProvider(), TestContext.Current.CancellationToken));

        Assert.Contains("2 MiB", exception.Message);
        Assert.DoesNotContain(FixtureApiKey, exception.Message);
    }

    [Fact]
    public async Task DiscoverAsync_RejectsOversizedResponseWithoutContentLength()
    {
        var responseBody = "{\"data\":[]}".PadRight(MaximumResponseBytes + 1, ' ');
        using var handler = new RecordingResponseHandler(HttpStatusCode.OK, responseBody,
            unknownLength: true);
        var service = new ModelDiscoveryService(new StaticProviderHttpClientFactory(handler));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.DiscoverAsync(CreateProvider(), TestContext.Current.CancellationToken));

        Assert.Contains("2 MiB", exception.Message);
    }

    private static AiProvider CreateProvider() =>
        new(Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
            new Uri("https://example.test/v1/"), FixtureApiKey, null, false, true,
            new Dictionary<string, string>());
}
