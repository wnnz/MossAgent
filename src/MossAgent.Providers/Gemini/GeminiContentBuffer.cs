using System.Text.Json;
using MossAgent.Application.Models;

namespace MossAgent.Providers.Gemini;

internal sealed class GeminiContentBuffer(ModelRequest? request)
{
    internal const int MaximumSignatureCharacters = 128 * 1024;
    internal const int MaximumTotalSignatureCharacters = 4 * MaximumSignatureCharacters;
    internal const int MaximumParts = 4096;
    private readonly List<JsonElement> _parts = [];
    private readonly HashSet<string> _nativeIds = new(StringComparer.Ordinal);
    private int _characters;
    private int _signatureCharacters;
    private int _callCount;
    private bool _hasSignature;

    public ModelFailed? Capture(JsonElement root, bool afterFinish)
    {
        if (GeminiEventMapper.SelectCandidate(root, out var candidate) is { } failure) return failure;
        if (candidate.ValueKind == JsonValueKind.Undefined
            || !candidate.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Object
            || !content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array) return null;
        foreach (var part in parts.EnumerateArray())
        {
            if (CapturePart(part, afterFinish) is { } partFailure) return partFailure;
        }
        return null;
    }

    private ModelFailed? CapturePart(JsonElement part, bool afterFinish)
    {
        if (afterFinish && (part.TryGetProperty("text", out var text) && text.GetString()?.Length > 0
            || part.EnumerateObject().Any(property => property.Name is not ("text" or "thought" or "thoughtSignature"))))
            return InvalidResponse();
        var signed = part.TryGetProperty("thoughtSignature", out var signature) && signature.ValueKind != JsonValueKind.Null;
        if (signed)
        {
            var signatureValue = signature.ValueKind == JsonValueKind.String ? signature.GetString() : null;
            if (string.IsNullOrWhiteSpace(signatureValue))
                return InvalidResponse();
            var signatureLength = signatureValue.Length;
            if (signatureLength > MaximumSignatureCharacters
                || signatureLength > MaximumTotalSignatureCharacters - _signatureCharacters) return InvalidResponse();
            _signatureCharacters += signatureLength;
            _hasSignature = true;
        }
        if (part.TryGetProperty("functionCall", out var call))
        {
            if (_callCount++ == 0 && request?.Model.ModelId.StartsWith("gemini-3", StringComparison.OrdinalIgnoreCase) == true
                && !signed) return InvalidResponse();
            if (call.TryGetProperty("id", out var identity) && identity.ValueKind != JsonValueKind.Null
                && !_nativeIds.Add(identity.GetString()!)) return InvalidResponse();
        }
        var length = part.GetRawText().Length + 1;
        if (_parts.Count >= MaximumParts || length > ModelContinuation.MaximumCharacters - 12 - _characters)
            return InvalidResponse();
        _characters += length;
        _parts.Add(part.Clone());
        return null;
    }

    public ModelContinuation? Complete()
    {
        if (!_hasSignature || request is null) return null;
        using var payload = JsonDocument.Parse("{\"parts\":[" + string.Join(",", _parts.Select(part => part.GetRawText())) + "]}");
        return new ModelContinuation(request.Provider.Id, request.Provider.Protocol, request.Model.ModelId, payload.RootElement);
    }

    public void Clear()
    {
        _parts.Clear();
        _nativeIds.Clear();
        _characters = 0;
        _signatureCharacters = 0;
        _callCount = 0;
        _hasSignature = false;
    }

    private static ModelFailed InvalidResponse() => new("invalid_response", ProviderText.Get("GeminiInvalidResponse"));
}
