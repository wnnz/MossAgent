using System.Collections.Immutable;
using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class ModelImageRequestTests
{
    [Theory]
    [MemberData(nameof(ModelImageTestFixture.Protocols), MemberType = typeof(ModelImageTestFixture))]
    public void NativeBlocks_PreserveTextAndImageOrder(ProviderProtocol protocol)
    {
        var first = ModelImageTestFixture.Image();
        var second = ModelImage.FromBytes("image/png", Convert.FromBase64String(ModelImageTestFixture.BluePng));
        var request = ModelImageTestFixture.Request(protocol) with
        {
            Messages = [new(ModelRole.User, "describe", Images: [first, second])]
        };

        using var document = JsonDocument.Parse(ModelImageTestFixture.Build(request));
        var content = ModelImageTestFixture.Content(document.RootElement, protocol);

        Assert.Equal(3, content.GetArrayLength());
        Assert.Equal("describe", content[0].GetProperty("text").GetString());
        if (protocol != ProviderProtocol.GeminiGenerateContent)
            Assert.Equal(protocol == ProviderProtocol.OpenAiResponses ? "input_text" : "text",
                content[0].GetProperty("type").GetString());
        ModelImageTestFixture.AssertImage(content[1], protocol, first);
        ModelImageTestFixture.AssertImage(content[2], protocol, second);
    }

    [Theory]
    [MemberData(nameof(ModelImageTestFixture.Protocols), MemberType = typeof(ModelImageTestFixture))]
    public void ImageOnlyMessages_AreNotDropped(ProviderProtocol protocol)
    {
        var request = ModelImageTestFixture.Request(protocol, "");
        using var document = JsonDocument.Parse(ModelImageTestFixture.Build(request));
        var content = ModelImageTestFixture.Content(document.RootElement, protocol);

        ModelImageTestFixture.AssertImage(Assert.Single(content.EnumerateArray()), protocol, request.Messages[0].Images[0]);
    }

    [Theory]
    [MemberData(nameof(ModelImageTestFixture.Protocols), MemberType = typeof(ModelImageTestFixture))]
    public void TextOnlyRequests_RetainTheirExistingWireShape(ProviderProtocol protocol)
    {
        var request = ModelImageTestFixture.Request(protocol) with { Messages = [new(ModelRole.User, "plain")] };
        using var document = JsonDocument.Parse(ModelImageTestFixture.Build(request));
        var content = ModelImageTestFixture.Content(document.RootElement, protocol);

        Assert.Equal("plain", protocol == ProviderProtocol.GeminiGenerateContent
            ? content[0].GetProperty("text").GetString() : content.GetString());
        Assert.DoesNotContain("base64", document.RootElement.GetRawText());
    }

    [Theory]
    [MemberData(nameof(ModelImageTestFixture.Protocols), MemberType = typeof(ModelImageTestFixture))]
    public void UnsupportedModels_AreRejectedWithoutLeakingPixelsOrCredentials(ProviderProtocol protocol)
    {
        var request = ModelImageTestFixture.Request(protocol);
        request = request with { Model = request.Model with { SupportsImages = false, SupportsFiles = true } };

        var error = Assert.Throws<ModelCapabilityException>(() => ModelImageTestFixture.Build(request));

        Assert.Equal("model_image_input_unsupported", error.Code);
        Assert.DoesNotContain(request.Provider.ApiKey, error.Message);
        Assert.DoesNotContain(request.Messages[0].Images[0].Base64Data, error.Message);
    }

    [Theory]
    [MemberData(nameof(ModelImageTestFixture.Protocols), MemberType = typeof(ModelImageTestFixture))]
    public void Images_CannotBeAttachedToOtherRolesOrToolMetadata(ProviderProtocol protocol)
    {
        var request = ModelImageTestFixture.Request(protocol);
        var user = request.Messages[0];
        foreach (var role in new[] { ModelRole.System, ModelRole.Assistant, ModelRole.Tool, ModelRole.ToolObservation })
            Assert.Throws<ArgumentException>(() => ModelImageTestFixture.Build(request with { Messages = [user with { Role = role }] }));
        Assert.Throws<ArgumentException>(() => ModelImageTestFixture.Build(request with { Messages = [user with { ToolCallId = "call" }] }));
        Assert.Throws<ArgumentException>(() => ModelImageTestFixture.Build(request with { Messages = [user with { ToolName = "tool" }] }));
        var call = new ModelToolCall("call", "tool", JsonSerializer.SerializeToElement(new { }));
        Assert.Throws<ArgumentException>(() => ModelImageTestFixture.Build(request with { Messages = [user with { ToolCalls = [call] }] }));
        var continuation = new ModelContinuation(request.Provider.Id, protocol, request.Model.ModelId,
            JsonSerializer.SerializeToElement(new { parts = new object[0] }));
        Assert.Throws<ArgumentException>(() => ModelImageTestFixture.Build(request with { Messages = [user with { Continuation = continuation }] }));
        Assert.Throws<ArgumentException>(() => ModelImageTestFixture.Build(request with { Messages = [user with { Images = [null!] }] }));
    }

    [Theory]
    [MemberData(nameof(ModelImageTestFixture.Protocols), MemberType = typeof(ModelImageTestFixture))]
    public void CountAndAggregateByteLimits_ApplyAcrossAllMessages(ProviderProtocol protocol)
    {
        var request = ModelImageTestFixture.Request(protocol);
        var ten = Enumerable.Repeat(new ModelMessage(ModelRole.User, "", Images: [ModelImageTestFixture.Image()]), 10).ToArray();
        ModelImageTestFixture.Build(request with { Messages = ten });
        Assert.Throws<ArgumentException>(() => ModelImageTestFixture.Build(request with { Messages = [.. ten, ten[0]] }));
        var large = ModelImage.FromBytes("image/png", new byte[ModelImage.MaximumByteCount]);
        var pair = new ModelMessage(ModelRole.User, "", Images: [large, large]);
        ModelImageTestFixture.Build(request with { Messages = [pair] });
        Assert.Throws<ArgumentException>(() => ModelImageTestFixture.Build(request with { Messages = [pair, new(ModelRole.User, "", Images: [large])] }));
        Assert.Throws<ArgumentException>(() => ModelImageTestFixture.Build(request with
        {
            Messages = [new(ModelRole.User, "", Images: Enumerable.Repeat(ModelImageTestFixture.Image(), 11).ToImmutableArray())]
        }));
    }

    [Theory]
    [MemberData(nameof(ModelImageTestFixture.Protocols), MemberType = typeof(ModelImageTestFixture))]
    public void AdvancedParameters_CannotReplaceManagedImageContent(ProviderProtocol protocol)
    {
        var request = ModelImageTestFixture.Request(protocol);
        request = request with { Model = request.Model with { AdvancedParametersJson = "{\"messages\":[],\"contents\":[],\"input\":[]}" } };

        var error = Assert.Throws<ModelCapabilityException>(() => ModelImageTestFixture.Build(request));

        Assert.Equal("invalid_model_parameters", error.Code);
        Assert.DoesNotContain(request.Provider.ApiKey, error.Message);
        Assert.DoesNotContain(request.Messages[0].Images[0].Base64Data, error.Message);
    }

    [Fact]
    public void GeminiImageValidation_DoesNotAddHistoryEnumerationPasses()
    {
        var request = ModelImageTestFixture.Request(ProviderProtocol.GeminiGenerateContent);
        var messages = new CountingModelMessageList(request.Messages);

        ModelImageTestFixture.Build(request with { Messages = messages });

        Assert.Equal(2, messages.EnumerationCount);
    }

    [Fact]
    public async Task SharedImage_CanBuildIndependentConcurrentRequests()
    {
        var request = ModelImageTestFixture.Request(ProviderProtocol.OpenAiResponses);
        var expected = ModelImageTestFixture.Build(request);
        var results = await Task.WhenAll(Enumerable.Range(0, 24)
            .Select(_ => Task.Run(() => ModelImageTestFixture.Build(request), TestContext.Current.CancellationToken)));

        Assert.All(results, result => Assert.Equal(expected, result));
        Assert.Equal(ModelImageTestFixture.RedPng, request.Messages[0].Images[0].Base64Data);
    }
}
