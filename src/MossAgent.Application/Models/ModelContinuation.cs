using System.Text.Json;
using System.Text.Json.Serialization;
using MossAgent.Domain;

namespace MossAgent.Application.Models;

public sealed class ModelContinuation
{
    public const int MaximumCharacters = 8 * 1024 * 1024;

    public ModelContinuation(Guid providerId, ProviderProtocol protocol, string modelId, JsonElement payload)
    {
        if (providerId == Guid.Empty || string.IsNullOrWhiteSpace(modelId))
            throw new ArgumentException("Continuation requires a provider and model identity.");
        if (payload.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Continuation requires an object payload.", nameof(payload));
        var length = payload.GetRawText().Length;
        if (length > MaximumCharacters)
            throw new ArgumentException("Continuation exceeds its size limit.", nameof(payload));
        ProviderId = providerId;
        Protocol = protocol;
        ModelId = modelId;
        CharacterCount = length;
        Payload = payload.Clone();
    }

    public Guid ProviderId { get; }
    public ProviderProtocol Protocol { get; }
    public string ModelId { get; }
    public int CharacterCount { get; }
    [JsonIgnore]
    public JsonElement Payload { get; }

    public override string ToString() => nameof(ModelContinuation);
}
