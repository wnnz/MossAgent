using System.Net;
using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class ModelImageStreamingTests
{
    [Theory]
    [MemberData(nameof(ModelImageTestFixture.Protocols), MemberType = typeof(ModelImageTestFixture))]
    public async Task ProductionStream_SendsNativeImagesOverLoopbackHttpAndParsesItsReply(ProviderProtocol protocol)
    {
        await using var server = new ImageInputLoopbackServer(Response(protocol), TestContext.Current.CancellationToken);
        using var handler = new SocketsHttpHandler { UseProxy = false, ConnectTimeout = TimeSpan.FromSeconds(5) };
        var provider = ProviderStreamingTestFixture.CreateProvider(protocol, handler);
        var request = ModelImageTestFixture.Request(protocol);
        request = request with { Provider = request.Provider with { BaseUri = server.BaseUri } };
        var events = new List<ModelEvent>();

        await foreach (var item in provider.StreamAsync(request, TestContext.Current.CancellationToken)) events.Add(item);
        await server.Completion.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(Assert.IsType<string>(server.RequestBody));
        var content = ModelImageTestFixture.Content(document.RootElement, protocol);
        ModelImageTestFixture.AssertImage(content[1], protocol, request.Messages[0].Images[0]);
        Assert.StartsWith("POST /v1/", server.RequestLine);
        Assert.Contains(events, item => item is TextDelta { Text: "image-ok" });
        Assert.Contains(events, item => item is ModelCompleted);
        Assert.DoesNotContain(events, item => item is ModelFailed);
    }

    [Theory]
    [MemberData(nameof(ModelImageTestFixture.Protocols), MemberType = typeof(ModelImageTestFixture))]
    public async Task UnsupportedModel_FailsBeforeAnyHttpRequest(ProviderProtocol protocol)
    {
        using var handler = new RecordingResponseHandler(HttpStatusCode.OK, "");
        var provider = ProviderStreamingTestFixture.CreateProvider(protocol, handler);
        var request = ModelImageTestFixture.Request(protocol);
        request = request with { Model = request.Model with { SupportsImages = false } };

        var events = new List<ModelEvent>();
        await foreach (var modelEvent in provider.StreamAsync(request, TestContext.Current.CancellationToken))
            events.Add(modelEvent);

        var failure = Assert.IsType<ModelFailed>(Assert.Single(events));
        Assert.Equal("model_image_input_unsupported", failure.Code);
        Assert.Null(handler.RequestUri);
        Assert.Null(handler.RequestBody);
    }

    [Theory]
    [MemberData(nameof(ModelImageTestFixture.Protocols), MemberType = typeof(ModelImageTestFixture))]
    public async Task CancelledImageRequest_DoesNotReachHttp(ProviderProtocol protocol)
    {
        using var handler = new RecordingResponseHandler(HttpStatusCode.OK, "");
        var provider = ProviderStreamingTestFixture.CreateProvider(protocol, handler);
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var unused in provider.StreamAsync(ModelImageTestFixture.Request(protocol), cancelled.Token)) { }
        });

        Assert.Null(handler.RequestUri);
    }

    private static string Response(ProviderProtocol protocol) => protocol switch
    {
        ProviderProtocol.OpenAiResponses =>
            "data: {\"type\":\"response.output_text.delta\",\"delta\":\"image-ok\"}\n\n"
            + "data: {\"type\":\"response.completed\",\"response\":{\"id\":\"image-response\",\"status\":\"completed\"}}\n\n",
        ProviderProtocol.OpenAiCompatible =>
            "data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\"image-ok\"},\"finish_reason\":null}]}\n\n"
            + "data: {\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n",
        ProviderProtocol.AnthropicMessages =>
            "data: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n\n"
            + "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"image-ok\"}}\n\n"
            + "data: {\"type\":\"message_stop\"}\n\n",
        ProviderProtocol.GeminiGenerateContent =>
            "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"image-ok\"}]},\"finishReason\":\"STOP\"}]}\n\n",
        _ => throw new ArgumentOutOfRangeException(nameof(protocol))
    };
}
