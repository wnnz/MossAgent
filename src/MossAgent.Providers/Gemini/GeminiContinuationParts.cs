using System.Text.Json;
using System.Text.Json.Nodes;
using MossAgent.Application.Models;

namespace MossAgent.Providers.Gemini;

internal static class GeminiContinuationParts
{
    public static JsonArray Restore(ModelContinuation continuation, ModelRequest request)
    {
        if (continuation.ProviderId != request.Provider.Id || continuation.Protocol != request.Provider.Protocol
            || continuation.ModelId != request.Model.ModelId)
            throw new InvalidOperationException("Model continuation does not match the current provider and model.");
        if (!continuation.Payload.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Model continuation does not contain valid content parts.");
        return JsonNode.Parse(parts.GetRawText())!.AsArray();
    }
}
