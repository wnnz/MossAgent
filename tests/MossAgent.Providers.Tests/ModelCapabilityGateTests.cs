using System.Net;
using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Providers.Tests;

/// <summary>
/// Joint model x protocol capability matrix verified at the common request
/// boundary: capable input reaches the HTTP layer; incapable or unknown
/// combinations fail before any request is sent.
/// </summary>
public sealed class ModelCapabilityGateTests
{
    private static string SuccessBody(ProviderProtocol protocol) => protocol switch
    {
        ProviderProtocol.OpenAiResponses =>
            "data: {\"type\":\"response.completed\",\"response\":{\"id\":\"done\"}}\n\n",
        ProviderProtocol.AnthropicMessages => "data: {\"type\":\"message_stop\"}\n\n",
        ProviderProtocol.OpenAiCompatible =>
            "data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\"hi\"},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n",
        ProviderProtocol.GeminiGenerateContent =>
            "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"hi\"}]},\"finishReason\":\"STOP\"}]}\n\n",
        _ => string.Empty
    };

    private static async Task<(List<ModelEvent> Events, RecordingResponseHandler Handler)> StreamAsync(
        ModelRequest request)
    {
        var handler = new RecordingResponseHandler(HttpStatusCode.OK,
            SuccessBody(request.Provider.Protocol), contentType: "text/event-stream");
        var provider = ProviderStreamingTestFixture.CreateProvider(request.Provider.Protocol, handler);
        var events = new List<ModelEvent>();
        await foreach (var modelEvent in provider.StreamAsync(request, TestContext.Current.CancellationToken))
            events.Add(modelEvent);
        return (events, handler);
    }

    private static ModelRequest WithImages(ModelRequest request) => request with
    {
        Messages = [new ModelMessage(ModelRole.User, "describe",
            Images: [ModelImageTestFixture.Image()])]
    };

    [Theory]
    [MemberData(nameof(ModelImageTestFixture.Protocols), MemberType = typeof(ModelImageTestFixture))]
    public async Task TextOnlyRequests_AlwaysReachHttp(ProviderProtocol protocol)
    {
        // Text needs no declared modality flag; even a capability-blind profile sends it.
        var request = ProviderStreamingTestFixture.CreateRequest(protocol);

        var (events, handler) = await StreamAsync(request);

        Assert.Equal(1, handler.RequestCount);
        Assert.Contains(events, modelEvent => modelEvent is ModelCompleted);
        Assert.DoesNotContain(events, modelEvent => modelEvent is ModelFailed);
    }

    [Theory]
    [MemberData(nameof(ModelImageTestFixture.Protocols), MemberType = typeof(ModelImageTestFixture))]
    public async Task ImageCapableModels_SendNativeImageBlocks(ProviderProtocol protocol)
    {
        var request = WithImages(ProviderStreamingTestFixture.CreateRequest(protocol));
        request = request with { Model = request.Model with { SupportsImages = true } };

        var (events, handler) = await StreamAsync(request);

        Assert.Equal(1, handler.RequestCount);
        Assert.Contains(events, modelEvent => modelEvent is ModelCompleted);
        Assert.Contains(ModelImageTestFixture.RedPng, handler.RequestBody);
        Assert.DoesNotContain(events, modelEvent => modelEvent is ModelFailed);
    }

    [Theory]
    [MemberData(nameof(ModelImageTestFixture.Protocols), MemberType = typeof(ModelImageTestFixture))]
    public async Task ImageIncapableModels_FailBeforeHttp(ProviderProtocol protocol)
    {
        var request = WithImages(ProviderStreamingTestFixture.CreateRequest(protocol));

        var (events, handler) = await StreamAsync(request);

        var failure = Assert.IsType<ModelFailed>(Assert.Single(events));
        Assert.Equal("model_image_input_unsupported", failure.Code);
        Assert.Equal(0, handler.RequestCount);
        Assert.DoesNotContain(request.Provider.ApiKey, failure.Message);
        Assert.DoesNotContain(ModelImageTestFixture.RedPng, failure.Message);
    }

    [Theory]
    [MemberData(nameof(ModelImageTestFixture.Protocols), MemberType = typeof(ModelImageTestFixture))]
    public async Task UnknownModels_FailClosedOnImages(ProviderProtocol protocol)
    {
        var request = WithImages(ProviderStreamingTestFixture.CreateRequest(protocol));
        request = request with { Model = request.Model with { ModelId = "totally-unknown-future-model" } };

        var (events, handler) = await StreamAsync(request);

        var failure = Assert.IsType<ModelFailed>(Assert.Single(events));
        Assert.Equal("model_image_input_unsupported", failure.Code);
        Assert.Equal(0, handler.RequestCount);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses, "fixture-model", "invalid value")]
    [InlineData(ProviderProtocol.OpenAiCompatible, "fixture-model", "invalid value")]
    [InlineData(ProviderProtocol.OpenAiCompatible, "fixture-model", "vendor-extreme!")]
    [InlineData(ProviderProtocol.AnthropicMessages, "claude-sonnet-4-6", "xhigh")]
    [InlineData(ProviderProtocol.AnthropicMessages, "claude-opus-4-5", "max")]
    [InlineData(ProviderProtocol.AnthropicMessages, "fixture-model", "vendor-extreme")]
    [InlineData(ProviderProtocol.GeminiGenerateContent, "gemini-3.8-flash", "xhigh")]
    [InlineData(ProviderProtocol.GeminiGenerateContent, "fixture-model", "high")]
    public async Task UnsupportedEffort_FailsBeforeHttp(
        ProviderProtocol protocol, string modelId, string effort)
    {
        var request = ProviderStreamingTestFixture.CreateRequest(protocol);
        request = request with { Model = request.Model with { ModelId = modelId, ReasoningEffort = effort } };

        var (events, handler) = await StreamAsync(request);

        var failure = Assert.IsType<ModelFailed>(Assert.Single(events));
        Assert.Equal("unsupported_reasoning_effort", failure.Code);
        Assert.Equal(0, handler.RequestCount);
        Assert.DoesNotContain(request.Provider.ApiKey, failure.Message);
        Assert.DoesNotContain(modelId, failure.Message);
        Assert.DoesNotContain(effort, failure.Message);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses, "fixture-model", "vendor-extreme")]
    [InlineData(ProviderProtocol.OpenAiCompatible, "fixture-model", "vendor-extreme")]
    [InlineData(ProviderProtocol.AnthropicMessages, "claude-sonnet-4-6", "max")]
    [InlineData(ProviderProtocol.AnthropicMessages, "claude-sonnet-4-5", "high")]
    [InlineData(ProviderProtocol.GeminiGenerateContent, "gemini-3.8-flash", "high")]
    public async Task SupportedEffort_ReachesHttp(ProviderProtocol protocol, string modelId, string effort)
    {
        var request = ProviderStreamingTestFixture.CreateRequest(protocol);
        request = request with { Model = request.Model with { ModelId = modelId, ReasoningEffort = effort } };

        var (events, handler) = await StreamAsync(request);

        Assert.Equal(1, handler.RequestCount);
        Assert.Contains(events, modelEvent => modelEvent is ModelCompleted);
        Assert.DoesNotContain(events, modelEvent => modelEvent is ModelFailed);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses, "{\"input\":[]}")]
    [InlineData(ProviderProtocol.OpenAiResponses, "{\"reasoning\":{\"effort\":\"high\"}}")]
    [InlineData(ProviderProtocol.OpenAiCompatible, "{\"messages\":[]}")]
    [InlineData(ProviderProtocol.OpenAiCompatible, "{\"max_completion_tokens\":999999}")]
    [InlineData(ProviderProtocol.AnthropicMessages, "{\"thinking\":{\"type\":\"disabled\"}}")]
    [InlineData(ProviderProtocol.AnthropicMessages, "{\"output_config\":{\"effort\":\"xhigh\"}}")]
    [InlineData(ProviderProtocol.GeminiGenerateContent, "{\"contents\":[]}")]
    [InlineData(ProviderProtocol.GeminiGenerateContent, "{\"generationConfig\":{\"maxOutputTokens\":1}}")]
    public async Task ManagedAdvancedParameters_FailBeforeHttp(ProviderProtocol protocol, string advancedJson)
    {
        var request = ProviderStreamingTestFixture.CreateRequest(protocol);
        request = request with { Model = request.Model with { AdvancedParametersJson = advancedJson } };

        var (events, handler) = await StreamAsync(request);

        var failure = Assert.IsType<ModelFailed>(Assert.Single(events));
        Assert.Equal("invalid_model_parameters", failure.Code);
        Assert.Equal(0, handler.RequestCount);
        Assert.DoesNotContain(request.Provider.ApiKey, failure.Message);
    }

    [Theory]
    [MemberData(nameof(ModelImageTestFixture.Protocols), MemberType = typeof(ModelImageTestFixture))]
    public async Task ManagedKeysCommonToAllProtocols_FailBeforeHttp(ProviderProtocol protocol)
    {
        var request = ProviderStreamingTestFixture.CreateRequest(protocol);
        request = request with { Model = request.Model with { AdvancedParametersJson = "{\"model\":\"override\"}" } };

        var (events, handler) = await StreamAsync(request);

        var failure = Assert.IsType<ModelFailed>(Assert.Single(events));
        Assert.Equal("invalid_model_parameters", failure.Code);
        Assert.Equal(0, handler.RequestCount);
    }

    [Theory]
    [MemberData(nameof(ModelImageTestFixture.Protocols), MemberType = typeof(ModelImageTestFixture))]
    public async Task MalformedAdvancedJson_FailsBeforeHttp(ProviderProtocol protocol)
    {
        var request = ProviderStreamingTestFixture.CreateRequest(protocol);
        request = request with { Model = request.Model with { AdvancedParametersJson = "{broken" } };

        var (events, handler) = await StreamAsync(request);

        var failure = Assert.IsType<ModelFailed>(Assert.Single(events));
        Assert.Equal("invalid_model_parameters", failure.Code);
        Assert.Equal(0, handler.RequestCount);
    }

    [Theory]
    [MemberData(nameof(ModelImageTestFixture.Protocols), MemberType = typeof(ModelImageTestFixture))]
    public async Task SafeExtensionParameters_ReachHttp(ProviderProtocol protocol)
    {
        var request = ProviderStreamingTestFixture.CreateRequest(protocol);
        request = request with { Model = request.Model with { AdvancedParametersJson = "{\"vendor_tag\":\"safe\"}" } };

        var (events, handler) = await StreamAsync(request);

        Assert.Equal(1, handler.RequestCount);
        Assert.Contains("vendor_tag", handler.RequestBody);
        Assert.DoesNotContain(events, modelEvent => modelEvent is ModelFailed);
    }

    [Fact]
    public void Check_ReportsTheOffendingParameterKey()
    {
        var request = ProviderStreamingTestFixture.CreateRequest(ProviderProtocol.OpenAiCompatible);
        request = request with { Model = request.Model with { AdvancedParametersJson = "{\"messages\":[]}" } };

        var failure = Assert.IsType<ModelFailed>(ModelCapabilityGate.Check(request));

        Assert.Equal("invalid_model_parameters", failure.Code);
        Assert.Contains("messages", failure.Message);
    }

    [Fact]
    public void Check_AcceptsTextAndValidParameters()
    {
        var request = ProviderStreamingTestFixture.CreateRequest(ProviderProtocol.GeminiGenerateContent);
        request = request with { Model = request.Model with { AdvancedParametersJson = "{\"safe_key\":1}" } };

        Assert.Null(ModelCapabilityGate.Check(request));
    }

    [Fact]
    public void Validate_ThrowsTypedCapabilityException()
    {
        var request = WithImages(ProviderStreamingTestFixture.CreateRequest(ProviderProtocol.OpenAiResponses));

        var error = Assert.Throws<ModelCapabilityException>(() => ModelCapabilityGate.Validate(request));

        Assert.Equal("model_image_input_unsupported", error.Code);
    }

    [Fact]
    public void Check_AllowsValidCustomOpenAiEffort()
    {
        var request = ProviderStreamingTestFixture.CreateRequest(ProviderProtocol.OpenAiResponses);
        request = request with { Model = request.Model with { ReasoningEffort = "vendor-extreme" } };

        Assert.Null(ModelCapabilityGate.Check(request));
    }
}
