using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Providers.ToolNames;

namespace MossAgent.Providers.Gemini;

internal static class GeminiEventMapper
{
    public static IReadOnlyList<ModelEvent> Map(
        JsonElement root,
        ProviderToolNameMap toolNames)
    {
        if (root.TryGetProperty("error", out var error))
        {
            return [ReadError(error)];
        }

        if (!root.TryGetProperty("candidates", out var candidates)
            || candidates.ValueKind != JsonValueKind.Array
            || candidates.GetArrayLength() == 0)
        {
            return [];
        }

        var candidate = candidates[0];
        var events = new List<ModelEvent>();
        if (candidate.TryGetProperty("content", out var content)
            && content.TryGetProperty("parts", out var parts))
        {
            foreach (var part in parts.EnumerateArray())
            {
                AddPart(events, part, toolNames);
            }
        }

        if (candidate.TryGetProperty("finishReason", out _))
        {
            events.Add(new ModelCompleted());
        }

        return events;
    }

    private static void AddPart(
        List<ModelEvent> events,
        JsonElement part,
        ProviderToolNameMap toolNames)
    {
        if (part.TryGetProperty("text", out var text))
        {
            events.Add(new TextDelta(text.GetString() ?? string.Empty));
        }

        if (!part.TryGetProperty("functionCall", out var call)
            || !call.TryGetProperty("name", out var name))
        {
            return;
        }

        var arguments = call.TryGetProperty("args", out var args)
            ? args.Clone()
            : JsonDocument.Parse("{}").RootElement.Clone();
        events.Add(new ToolCallCompleted(
            Guid.NewGuid().ToString("N"),
            toolNames.Decode(name.GetString() ?? string.Empty), arguments));
    }

    private static ModelEvent ReadError(JsonElement error)
    {
        var code = error.TryGetProperty("code", out var codeValue)
            ? codeValue.ToString()
            : "gemini_error";
        var message = error.TryGetProperty("message", out var messageValue)
            ? messageValue.GetString() ?? "Gemini 请求失败。"
            : "Gemini 请求失败。";
        return new ModelFailed(code, message);
    }
}
