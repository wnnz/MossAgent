using System.Net;
using System.Text;
using MossAgent.Application.Networking;
using MossAgent.Application.Models;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class ModelDiscoveryServiceTests
{
    private const string FixtureApiKey = "fixture-api-key";

    [Fact]
    public async Task DiscoverAsync_UsesBearerKeyAndSortsStandardModels()
    {
        using var handler = new RecordingResponseHandler(HttpStatusCode.OK, """
            {"data":[{"id":"z-model","display_name":"Zulu"},{"id":"a-model","display_name":"Alpha"},{"display_name":"missing-id"}]}
            """);
        var service = new ModelDiscoveryService(new StaticProviderHttpClientFactory(handler));

        var models = await service.DiscoverAsync(
            CreateProvider(ProviderProtocol.OpenAiResponses), CancellationToken.None);

        Assert.Collection(models,
            model => Assert.Equal(new DiscoveredModel("a-model", "Alpha"), model),
            model => Assert.Equal(new DiscoveredModel("z-model", "Zulu"), model));
        Assert.Equal(HttpMethod.Get, handler.RequestMethod);
        Assert.Equal(new Uri("https://example.test/v1/models"), handler.RequestUri);
        Assert.Equal($"Bearer {FixtureApiKey}", handler.AuthorizationHeader);
    }

    [Fact]
    public async Task DiscoverAsync_RespectsResponseCharset()
    {
        using var handler = new RecordingResponseHandler(HttpStatusCode.OK,
            """{"data":[{"id":"model","display_name":"中文模型"}]}""",
            responseEncoding: Encoding.Unicode);
        var service = new ModelDiscoveryService(new StaticProviderHttpClientFactory(handler));

        var models = await service.DiscoverAsync(
            CreateProvider(ProviderProtocol.OpenAiResponses), TestContext.Current.CancellationToken);

        Assert.Equal("中文模型", Assert.Single(models).DisplayName);
    }

    [Fact]
    public async Task DiscoverAsync_NormalizesGeminiModelNamesAndUsesGoogleApiKey()
    {
        using var handler = new RecordingResponseHandler(HttpStatusCode.OK, """
            {"models":[{"name":"models/gemini-3.8-flash","displayName":"Gemini 3.8 Flash"}]}
            """);
        var service = new ModelDiscoveryService(new StaticProviderHttpClientFactory(handler));

        var models = await service.DiscoverAsync(
            CreateProvider(ProviderProtocol.GeminiGenerateContent), CancellationToken.None);

        Assert.Equal(new DiscoveredModel("gemini-3.8-flash", "Gemini 3.8 Flash"),
            Assert.Single(models));
        Assert.Equal(FixtureApiKey, handler.GoogleApiKeyHeader);
        Assert.Equal(new Uri("https://example.test/v1/models"), handler.RequestUri);
    }

    [Fact]
    public async Task DiscoverAsync_GeminiFollowsPagesAndKeepsUniqueModels()
    {
        using var handler = new PaginatedModelResponseHandler(
            """{"models":[{"name":"models/gemini-3.8-pro","displayName":"Pro"}],"nextPageToken":"page+2/="}""",
            """{"models":[{"name":"models/gemini-3.8-flash","displayName":"Flash"},{"name":"models/gemini-3.8-pro","displayName":"Updated Pro"}]}""");
        var service = new ModelDiscoveryService(new StaticProviderHttpClientFactory(handler));

        var models = await service.DiscoverAsync(
            CreateProvider(ProviderProtocol.GeminiGenerateContent), CancellationToken.None);

        Assert.Collection(models,
            model => Assert.Equal(new DiscoveredModel("gemini-3.8-flash", "Flash"), model),
            model => Assert.Equal(new DiscoveredModel("gemini-3.8-pro", "Pro"), model));
        Assert.Equal(2, handler.RequestUris.Count);
        Assert.Equal(new Uri("https://example.test/v1/models"), handler.RequestUris[0]);
        Assert.Equal("?pageToken=page%2B2%2F%3D", handler.RequestUris[1].Query);
    }

    [Fact]
    public async Task DiscoverAsync_GeminiRejectsRepeatedPageTokens()
    {
        using var handler = new PaginatedModelResponseHandler(
            """{"models":[],"nextPageToken":"repeat"}""",
            """{"models":[],"nextPageToken":"repeat"}""");
        var service = new ModelDiscoveryService(new StaticProviderHttpClientFactory(handler));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.DiscoverAsync(
                CreateProvider(ProviderProtocol.GeminiGenerateContent), CancellationToken.None));

        Assert.Contains("分页标记", exception.Message);
        Assert.Equal(2, handler.RequestUris.Count);
    }

    [Fact]
    public async Task DiscoverAsync_GeminiCapsPageCount()
    {
        var responses = Enumerable.Range(1, 16)
            .Select(index => "{\"models\":[],\"nextPageToken\":\"page-" + index + "\"}")
            .ToArray();
        using var handler = new PaginatedModelResponseHandler(responses);
        var service = new ModelDiscoveryService(new StaticProviderHttpClientFactory(handler));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.DiscoverAsync(
                CreateProvider(ProviderProtocol.GeminiGenerateContent), CancellationToken.None));

        Assert.Contains("安全分页上限", exception.Message);
        Assert.Equal(16, handler.RequestUris.Count);
    }

    [Fact]
    public async Task DiscoverAsync_UsesAnthropicHeaders()
    {
        using var handler = new RecordingResponseHandler(HttpStatusCode.OK,
            """{"data":[{"id":"claude-model"}]}""");
        var service = new ModelDiscoveryService(new StaticProviderHttpClientFactory(handler));

        var models = await service.DiscoverAsync(
            CreateProvider(ProviderProtocol.AnthropicMessages), CancellationToken.None);

        Assert.Equal("claude-model", Assert.Single(models).Id);
        Assert.Equal(FixtureApiKey, handler.AnthropicApiKeyHeader);
        Assert.Equal("2023-06-01", handler.AnthropicVersionHeader);
    }

    [Fact]
    public async Task DiscoverAsync_RedactsApiKeyFromErrorBody()
    {
        using var handler = new RecordingResponseHandler(
            HttpStatusCode.Unauthorized, $"{{\"error\":\"{FixtureApiKey}\"}}");
        var service = new ModelDiscoveryService(new StaticProviderHttpClientFactory(handler));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await service.DiscoverAsync(
                CreateProvider(ProviderProtocol.OpenAiResponses), CancellationToken.None);
        });

        Assert.DoesNotContain(FixtureApiKey, exception.Message);
        Assert.Contains("***", exception.Message);
    }

    [Fact]
    public async Task DiscoverAsync_RedactsCustomHeaderValuesFromErrorBody()
    {
        const string headerSecret = "custom-header-secret";
        using var handler = new RecordingResponseHandler(
            HttpStatusCode.BadRequest, $"{{\"error\":\"{headerSecret}\"}}");
        var provider = CreateProvider(ProviderProtocol.OpenAiResponses) with
        {
            Headers = new Dictionary<string, string> { ["X-Custom"] = headerSecret }
        };
        var service = new ModelDiscoveryService(new StaticProviderHttpClientFactory(handler));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await service.DiscoverAsync(provider, CancellationToken.None);
        });

        Assert.DoesNotContain(headerSecret, exception.Message, StringComparison.Ordinal);
        Assert.Contains("***", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiscoverAsync_DoesNotExposeRequestDetailsWhenTransportFails()
    {
        using var handler = new RecordingResponseHandler(
            HttpStatusCode.OK, string.Empty,
            new HttpRequestException($"https://example.test/models?key={FixtureApiKey}"));
        var service = new ModelDiscoveryService(new StaticProviderHttpClientFactory(handler));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await service.DiscoverAsync(
                CreateProvider(ProviderProtocol.OpenAiResponses), CancellationToken.None);
        });

        Assert.DoesNotContain(FixtureApiKey, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("https://", exception.Message, StringComparison.Ordinal);
        Assert.Contains("无法连接", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiscoverAsync_HandlesProviderWithoutApiKey()
    {
        using var handler = new RecordingResponseHandler(
            HttpStatusCode.Unauthorized, "{\"error\":\"missing authorization\"}");
        var service = new ModelDiscoveryService(new StaticProviderHttpClientFactory(handler));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await service.DiscoverAsync(
                CreateProvider(ProviderProtocol.OpenAiResponses, string.Empty),
                CancellationToken.None);
        });

        Assert.Contains("missing authorization", exception.Message);
    }

    private static AiProvider CreateProvider(
        ProviderProtocol protocol,
        string apiKey = FixtureApiKey) =>
        new(Guid.NewGuid(), "fixture", protocol, new Uri("https://example.test/v1/"),
            apiKey, null, false, true, new Dictionary<string, string>());
}
