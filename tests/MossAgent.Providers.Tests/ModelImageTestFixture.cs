using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Providers.Anthropic;
using MossAgent.Providers.Gemini;
using MossAgent.Providers.OpenAi;
using MossAgent.Providers.ToolNames;
using Xunit;

namespace MossAgent.Providers.Tests;

public static class ModelImageTestFixture
{
    public const string RedPng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC";
    public const string BluePng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGNgYPgPAAEDAQAIicLsAAAAAElFTkSuQmCC";

    public static TheoryData<ProviderProtocol> Protocols => new()
    {
        ProviderProtocol.OpenAiResponses, ProviderProtocol.OpenAiCompatible,
        ProviderProtocol.AnthropicMessages, ProviderProtocol.GeminiGenerateContent
    };

    public static ModelImage Image() => ModelImage.FromBytes("image/png", Convert.FromBase64String(RedPng));

    public static ModelRequest Request(ProviderProtocol protocol, string text = "describe")
    {
        var request = ProviderStreamingTestFixture.CreateRequest(protocol);
        return request with
        {
            Model = request.Model with { SupportsImages = true, SupportsFiles = false },
            Messages = [new(ModelRole.User, text, Images: [Image()])]
        };
    }

    public static string Build(ModelRequest request)
    {
        var names = new ProviderToolNameMap(request.Tools);
        return request.Provider.Protocol switch
        {
            ProviderProtocol.OpenAiResponses => OpenAiRequestBuilder.Build(request, names),
            ProviderProtocol.OpenAiCompatible => OpenAiCompatibleRequestBuilder.Build(request, names),
            ProviderProtocol.AnthropicMessages => AnthropicRequestBuilder.Build(request, names),
            ProviderProtocol.GeminiGenerateContent => GeminiRequestBuilder.Build(request, names),
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };
    }

    public static JsonElement Content(JsonElement root, ProviderProtocol protocol) => protocol switch
    {
        ProviderProtocol.OpenAiResponses => root.GetProperty("input")[0].GetProperty("content"),
        ProviderProtocol.GeminiGenerateContent => root.GetProperty("contents")[0].GetProperty("parts"),
        _ => root.GetProperty("messages")[0].GetProperty("content")
    };

    public static void AssertImage(JsonElement block, ProviderProtocol protocol, ModelImage image)
    {
        if (protocol == ProviderProtocol.GeminiGenerateContent)
        {
            var inline = block.GetProperty("inlineData");
            Assert.Equal(image.MediaType, inline.GetProperty("mimeType").GetString());
            Assert.Equal(image.Base64Data, inline.GetProperty("data").GetString());
        }
        else if (protocol == ProviderProtocol.AnthropicMessages)
        {
            Assert.Equal("image", block.GetProperty("type").GetString());
            var source = block.GetProperty("source");
            Assert.Equal("base64", source.GetProperty("type").GetString());
            Assert.Equal(image.MediaType, source.GetProperty("media_type").GetString());
            Assert.Equal(image.Base64Data, source.GetProperty("data").GetString());
        }
        else
        {
            Assert.Equal(protocol == ProviderProtocol.OpenAiResponses ? "input_image" : "image_url",
                block.GetProperty("type").GetString());
            var imageUrl = block.GetProperty("image_url");
            var url = protocol == ProviderProtocol.OpenAiResponses ? imageUrl.GetString() : imageUrl.GetProperty("url").GetString();
            Assert.Equal($"data:{image.MediaType};base64,{image.Base64Data}", url);
            Assert.Equal("auto", protocol == ProviderProtocol.OpenAiResponses
                ? block.GetProperty("detail").GetString() : imageUrl.GetProperty("detail").GetString());
        }
    }
}
