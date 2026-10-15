using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal static class ProviderRoutingTestProtocol
{
    public static string Models(ProviderProtocol protocol) => protocol == ProviderProtocol.GeminiGenerateContent
        ? """{"models":[{"name":"models/fixture-embedding","supportedGenerationMethods":["embedContent"]},{"name":"models/fixture-model","supportedGenerationMethods":["generateContent"]}]}"""
        : """{"data":[{"id":"fixture-model"}]}""";

    public static string StreamPath(ProviderProtocol protocol) => protocol switch
    {
        ProviderProtocol.OpenAiResponses => "responses",
        ProviderProtocol.OpenAiCompatible => "chat/completions",
        ProviderProtocol.GeminiGenerateContent => "models/fixture-model:streamGenerateContent?alt=sse",
        _ => throw new ArgumentOutOfRangeException(nameof(protocol))
    };

    public static string Stream(ProviderProtocol protocol) => protocol switch
    {
        ProviderProtocol.OpenAiResponses =>
            "data: {\"type\":\"response.output_text.delta\",\"delta\":\"hello\"}\n\n"
            + "data: {\"type\":\"response.completed\",\"response\":{\"id\":\"fixture-response\"}}\n\n"
            + "data: [DONE]\n\n",
        ProviderProtocol.OpenAiCompatible =>
            "data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\"hello\"}}]}\n\n"
            + "data: {\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n"
            + "data: [DONE]\n\n",
        ProviderProtocol.GeminiGenerateContent =>
            "data: {\"candidates\":[{\"index\":0,\"content\":{\"role\":\"model\",\"parts\":[{\"text\":\"hello\"}]},\"finishReason\":\"STOP\"}]}\n\n",
        _ => throw new ArgumentOutOfRangeException(nameof(protocol))
    };
}
