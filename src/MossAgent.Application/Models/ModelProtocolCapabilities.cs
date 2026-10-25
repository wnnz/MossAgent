using MossAgent.Domain;

namespace MossAgent.Application.Models;

/// <summary>
/// The protocol side of the model x protocol capability matrix: whether a wire
/// protocol can carry a given input modality at all. Per-model flags
/// (<see cref="Domain.ModelProfile"/>) decide support; this table decides
/// whether the protocol could even transport it.
/// </summary>
public static class ModelProtocolCapabilities
{
    /// <summary>
    /// All current protocols can carry native image blocks. Fail closed for
    /// protocol values this build does not know.
    /// </summary>
    public static bool AcceptsImages(ProviderProtocol protocol) => protocol switch
    {
        ProviderProtocol.OpenAiResponses or ProviderProtocol.OpenAiCompatible
            or ProviderProtocol.AnthropicMessages or ProviderProtocol.GeminiGenerateContent => true,
        _ => false
    };

    /// <summary>
    /// Document attachments are transported as bounded extracted text inside the
    /// normal message content, so they only require text input, which every
    /// supported protocol/model provides. <see cref="Domain.ModelProfile.SupportsFiles"/>
    /// describes native file input and stays an advisory/authorization flag
    /// until a native document transport exists.
    /// </summary>
    public static bool AcceptsExtractedTextDocuments(ProviderProtocol protocol) =>
        Enum.IsDefined(protocol);
}
