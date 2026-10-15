using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Providers.ToolNames;

namespace MossAgent.Providers.OpenAi;

internal sealed class OpenAiCompatibleStreamParser(ProviderToolNameMap toolNames)
{
    private readonly OpenAiCompatibleToolCallAccumulator _calls = new(toolNames);
    private string? _responseId;
    private bool _hasFinishSignal;
    public bool IsTerminal { get; private set; }

    public IReadOnlyList<ModelEvent> Parse(string data)
    {
        if (IsTerminal) return [];
        if (data == "[DONE]")
        {
            _hasFinishSignal = true;
            return Complete();
        }
        try
        {
            using var document = JsonDocument.Parse(data);
            return ParseRoot(document.RootElement);
        }
        catch (JsonException)
        {
            return Fail("invalid_response", "CompatibleInvalidResponse");
        }
    }

    public IReadOnlyList<ModelEvent> Complete()
    {
        if (IsTerminal) return [];
        IsTerminal = true;
        if (!_hasFinishSignal) return Failure("incomplete_response", "CompatibleIncompleteResponse");
        var events = _calls.Drain().ToList();
        if (events.Any(item => item is ModelFailed)) return events;
        events.Add(new ModelCompleted(_responseId));
        return events;
    }

    private IReadOnlyList<ModelEvent> ParseRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return Fail("invalid_response", "CompatibleInvalidResponse");
        _responseId ??= ReadString(root, "id");
        if (root.TryGetProperty("error", out var error))
        {
            IsTerminal = true;
            return [new ModelFailed(ReadString(error, "code") ?? "openai_compatible_error",
                ReadString(error, "message") ?? ProviderText.Get("CompatibleRequestFailed"))];
        }
        if (!root.TryGetProperty("choices", out var choices)) return [];
        if (choices.ValueKind != JsonValueKind.Array) return Fail("invalid_response", "CompatibleInvalidResponse");
        var events = new List<ModelEvent>();
        foreach (var choice in choices.EnumerateArray())
        {
            if (!TryReadChoiceIndex(choice, out var index)) return Fail("invalid_response", "CompatibleInvalidResponse");
            if (index != 0) continue;
            if (ParseChoice(choice, events) is { } failure) return failure;
        }
        return events;
    }

    private IReadOnlyList<ModelEvent>? ParseChoice(JsonElement choice, List<ModelEvent> events)
    {
        if (choice.TryGetProperty("delta", out var delta) && delta.ValueKind != JsonValueKind.Null)
        {
            if (delta.ValueKind != JsonValueKind.Object) return Fail("invalid_response", "CompatibleInvalidResponse");
            if (_hasFinishSignal && (ReadString(delta, "content") is { Length: > 0 }
                || delta.TryGetProperty("tool_calls", out var calls) && calls.ValueKind != JsonValueKind.Null
                && (calls.ValueKind != JsonValueKind.Array || calls.GetArrayLength() != 0)))
                return Fail("invalid_response", "CompatibleInvalidResponse");
            if (_calls.Capture(delta) is { } failure)
            {
                IsTerminal = true;
                return [failure];
            }
            if (ReadString(delta, "content") is { Length: > 0 } text) events.Add(new TextDelta(text));
        }
        if (ReadString(choice, "finish_reason") is { Length: > 0 } finish)
        {
            _hasFinishSignal = true;
            if (finish == "tool_calls")
            {
                var completed = _calls.Drain();
                if (completed.Any(item => item is ModelFailed))
                {
                    IsTerminal = true;
                    return completed;
                }
                events.AddRange(completed);
            }
        }
        return null;
    }

    private IReadOnlyList<ModelEvent> Fail(string code, string resource)
    {
        IsTerminal = true;
        return Failure(code, resource);
    }

    private static IReadOnlyList<ModelEvent> Failure(string code, string resource) =>
        [new ModelFailed(code, ProviderText.Get(resource))];

    private static bool TryReadChoiceIndex(JsonElement choice, out int index)
    {
        index = 0;
        if (choice.ValueKind != JsonValueKind.Object) return false;
        return !choice.TryGetProperty("index", out var value)
            || value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out index) && index >= 0;
    }

    private static string? ReadString(JsonElement root, string propertyName) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
