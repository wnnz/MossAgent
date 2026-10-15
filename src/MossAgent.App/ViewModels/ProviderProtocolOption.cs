using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed record ProviderProtocolOption(ProviderProtocol Protocol, string DisplayName)
{
    public static IReadOnlyList<ProviderProtocolOption> All { get; } =
    [
        new(ProviderProtocol.OpenAiResponses, "OpenAI Responses API"),
        new(ProviderProtocol.OpenAiCompatible, "OpenAI Chat Completions 兼容"),
        new(ProviderProtocol.AnthropicMessages, "Anthropic Messages API"),
        new(ProviderProtocol.GeminiGenerateContent, "Gemini generateContent API")
    ];

    public static ProviderProtocolOption FromProtocol(ProviderProtocol protocol) =>
        All.Single(option => option.Protocol == protocol);
}
