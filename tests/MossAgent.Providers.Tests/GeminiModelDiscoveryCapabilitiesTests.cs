using System.Net;
using MossAgent.Application.Models;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class GeminiModelDiscoveryCapabilitiesTests
{
    [Theory]
    [InlineData("generateContent")]
    [InlineData("streamGenerateContent")]
    public async Task ExplicitGenerationCapability_DiscoversModelsWithoutGuessingFromTheirNames(string method)
    {
        using var handler = new RecordingResponseHandler(HttpStatusCode.OK,
            "{\"models\":[{\"name\":\"models/custom-model\",\"supportedGenerationMethods\":[\"" + method + "\"]}]}");
        var service = new ModelDiscoveryService(new StaticProviderHttpClientFactory(handler));

        var models = await service.DiscoverAsync(Provider(), TestContext.Current.CancellationToken);

        Assert.Equal(new DiscoveredModel("custom-model", "custom-model"), Assert.Single(models));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[\"embedContent\"]")]
    [InlineData("[\"predict\",\"countTokens\"]")]
    [InlineData("[\"generateContentPreview\"]")]
    public async Task ExplicitNonGenerationCapability_IsNotOfferedForConversation(string methods)
    {
        using var handler = new RecordingResponseHandler(HttpStatusCode.OK, Models(methods));
        var service = new ModelDiscoveryService(new StaticProviderHttpClientFactory(handler));

        var models = await service.DiscoverAsync(Provider(), TestContext.Current.CancellationToken);

        Assert.Empty(models);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("\"generateContent\"")]
    [InlineData("[null]")]
    [InlineData("[42,\"generateContent\"]")]
    [InlineData("[\"generateContent\",false]")]
    public async Task MalformedExplicitCapabilities_RejectTheWholeResponseWithoutEchoingContent(string methods)
    {
        using var handler = new RecordingResponseHandler(HttpStatusCode.OK, Models(methods));
        var service = new ModelDiscoveryService(new StaticProviderHttpClientFactory(handler));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.DiscoverAsync(Provider(), TestContext.Current.CancellationToken));

        Assert.DoesNotContain("fixture-model-secret", exception.Message);
        Assert.DoesNotContain(methods, exception.Message);
    }

    [Fact]
    public async Task Pagination_FiltersBeforeDeduplicationAndKeepsTheFirstGenerativeSnapshot()
    {
        using var handler = new PaginatedModelResponseHandler(
            """
            {"models":[{"name":"models/shared","displayName":"Embedding","supportedGenerationMethods":["embedContent"]},
            {"name":"models/pro","displayName":"Pro","supportedGenerationMethods":["generateContent"]}],"nextPageToken":"next+page"}
            """,
            """
            {"models":[{"name":"models/shared","displayName":"Chat","supportedGenerationMethods":["generateContent"]},
            {"name":"models/pro","displayName":"Updated","supportedGenerationMethods":["streamGenerateContent"]}]}
            """);
        var service = new ModelDiscoveryService(new StaticProviderHttpClientFactory(handler));

        var models = await service.DiscoverAsync(Provider(), TestContext.Current.CancellationToken);

        Assert.Equal([new DiscoveredModel("shared", "Chat"), new DiscoveredModel("pro", "Pro")], models);
        Assert.Equal(2, handler.RequestUris.Count);
        Assert.Equal("?pageToken=next%2Bpage", handler.RequestUris[1].Query);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses)]
    [InlineData(ProviderProtocol.OpenAiCompatible)]
    public async Task OtherProtocols_DoNotInterpretGeminiSpecificMetadata(ProviderProtocol protocol)
    {
        using var handler = new RecordingResponseHandler(HttpStatusCode.OK,
            """{"data":[{"id":"custom-model","supportedGenerationMethods":["embedContent"]}]}""");
        var service = new ModelDiscoveryService(new StaticProviderHttpClientFactory(handler));

        var models = await service.DiscoverAsync(Provider(protocol), TestContext.Current.CancellationToken);

        Assert.Equal("custom-model", Assert.Single(models).Id);
    }

    private static string Models(string methods) =>
        "{\"models\":[{\"name\":\"models/fixture-model-secret\",\"supportedGenerationMethods\":" + methods + "}]}";

    private static AiProvider Provider(ProviderProtocol protocol = ProviderProtocol.GeminiGenerateContent) =>
        new(Guid.NewGuid(), "fixture", protocol, new Uri("https://fixture.example.test/v1/"),
            string.Empty, null, false, true, new Dictionary<string, string>());
}
