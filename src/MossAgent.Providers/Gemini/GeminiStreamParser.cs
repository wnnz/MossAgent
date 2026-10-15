using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Providers.ToolNames;

namespace MossAgent.Providers.Gemini;

internal sealed class GeminiStreamParser(ProviderToolNameMap toolNames, ModelRequest? request = null)
{
    private readonly GeminiToolCallBuffer _calls = new();
    private readonly GeminiContentBuffer _content = new(request);
    private bool _hasFinishSignal;
    private string? _responseId;
    public bool IsTerminal { get; private set; }

    public IReadOnlyList<ModelEvent> Parse(string data)
    {
        if (IsTerminal) return [];
        if (data == "[DONE]") return Complete();
        try
        {
            using var document = JsonDocument.Parse(data);
            return ParseRoot(document.RootElement);
        }
        catch (JsonException)
        {
            return Fail(new ModelFailed("invalid_response", ProviderText.Get("GeminiInvalidResponse")));
        }
    }

    public IReadOnlyList<ModelEvent> Complete()
    {
        if (IsTerminal) return [];
        if (!_hasFinishSignal)
            return Fail(new ModelFailed("incomplete_response", ProviderText.Get("GeminiIncompleteResponse")));
        IsTerminal = true;
        var continuation = _content.Complete();
        _content.Clear();
        return [.. _calls.Drain(), new ModelCompleted(_responseId, continuation)];
    }

    private IReadOnlyList<ModelEvent> ParseRoot(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("responseId", out var responseId))
        {
            if (responseId.ValueKind != JsonValueKind.String
                || responseId.GetString()!.Length > GeminiToolCallBuffer.MaximumIdentityCharacters)
                return InvalidResponse();
            _responseId ??= responseId.GetString();
        }
        var mapped = GeminiEventMapper.Map(root, toolNames);
        if (mapped.OfType<ModelFailed>().FirstOrDefault() is { } failure) return Fail(failure);
        if (_hasFinishSignal && mapped.Any(item => item is TextDelta or ToolCallCompleted))
            return InvalidResponse();
        if (_content.Capture(root, _hasFinishSignal) is { } contentFailure) return Fail(contentFailure);
        if (mapped.All(static item => item is TextDelta)) return mapped;
        var events = new List<ModelEvent>();
        foreach (var item in mapped)
        {
            if (item is ToolCallCompleted call)
            {
                if (_calls.Capture(call) is { } callFailure) return Fail(callFailure);
            }
            else if (item is ModelCompleted) _hasFinishSignal = true;
            else events.Add(item);
        }
        return events;
    }

    private IReadOnlyList<ModelEvent> InvalidResponse() =>
        Fail(new ModelFailed("invalid_response", ProviderText.Get("GeminiInvalidResponse")));

    private IReadOnlyList<ModelEvent> Fail(ModelFailed failure)
    {
        IsTerminal = true;
        _calls.Clear();
        _content.Clear();
        return [failure];
    }
}
