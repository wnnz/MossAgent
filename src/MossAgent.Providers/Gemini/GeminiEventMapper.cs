using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Providers.ToolNames;

namespace MossAgent.Providers.Gemini;

internal static class GeminiEventMapper
{
    public static IReadOnlyList<ModelEvent> Map(JsonElement root, ProviderToolNameMap toolNames)
    {
        if (root.ValueKind != JsonValueKind.Object) return [InvalidResponse()];
        if (root.TryGetProperty("error", out var error)) return [ReadError(error)];
        if (ReadPromptFeedback(root) is { } blocked) return [blocked];
        if (SelectCandidate(root, out var candidate) is { } invalid) return [invalid];
        if (candidate.ValueKind == JsonValueKind.Undefined) return [];
        var finish = ReadFinish(candidate);
        if (finish is ModelFailed failure) return [failure];
        var events = new List<ModelEvent>();
        if (ReadParts(candidate, events, toolNames) is { } partsFailure) return [partsFailure];
        if (finish is not null) events.Add(finish);
        return events;
    }

    internal static ModelFailed? SelectCandidate(JsonElement root, out JsonElement selected)
    {
        selected = default;
        if (!root.TryGetProperty("candidates", out var candidates)) return null;
        if (candidates.ValueKind != JsonValueKind.Array) return InvalidResponse();
        foreach (var candidate in candidates.EnumerateArray())
        {
            if (candidate.ValueKind != JsonValueKind.Object) return InvalidResponse();
            var index = 0;
            if (candidate.TryGetProperty("index", out var value)
                && (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out index) || index < 0))
                return InvalidResponse();
            if (index != 0) continue;
            if (selected.ValueKind != JsonValueKind.Undefined) return InvalidResponse();
            selected = candidate;
        }
        return null;
    }

    private static ModelFailed? ReadPromptFeedback(JsonElement root)
    {
        if (!root.TryGetProperty("promptFeedback", out var feedback) || feedback.ValueKind == JsonValueKind.Null)
            return null;
        if (feedback.ValueKind != JsonValueKind.Object) return InvalidResponse();
        if (!feedback.TryGetProperty("blockReason", out var reason)) return null;
        if (reason.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(reason.GetString()))
            return InvalidResponse();
        return reason.GetString() == "BLOCK_REASON_UNSPECIFIED" ? null
            : Failure("provider_blocked", "GeminiBlockedResponse");
    }

    private static ModelEvent? ReadFinish(JsonElement candidate)
    {
        if (!candidate.TryGetProperty("finishReason", out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.String) return InvalidResponse();
        return value.GetString() switch
        {
            "STOP" => new ModelCompleted(),
            "MAX_TOKENS" => Failure("output_limit", "GeminiOutputLimit"),
            "SAFETY" or "RECITATION" or "BLOCKLIST" or "PROHIBITED_CONTENT" or "SPII"
                or "IMAGE_SAFETY" or "IMAGE_PROHIBITED_CONTENT" or "IMAGE_RECITATION"
                or "ESCALATION" or "PUP_LIMITED_DISABLED" => Failure("provider_blocked", "GeminiBlockedResponse"),
            "MALFORMED_FUNCTION_CALL" or "UNEXPECTED_TOOL_CALL" => Failure("invalid_tool_arguments", "GeminiInvalidToolCall"),
            "TOO_MANY_TOOL_CALLS" => Failure("tool_calls_limit", "GeminiToolCallsLimit"),
            _ => InvalidResponse()
        };
    }

    private static ModelFailed? ReadParts(JsonElement candidate, List<ModelEvent> events, ProviderToolNameMap toolNames)
    {
        if (!candidate.TryGetProperty("content", out var content) || content.ValueKind == JsonValueKind.Null)
            return null;
        if (content.ValueKind != JsonValueKind.Object) return InvalidResponse();
        if (content.TryGetProperty("role", out var role)
            && (role.ValueKind != JsonValueKind.String || role.GetString() != "model")) return InvalidResponse();
        if (!content.TryGetProperty("parts", out var parts) || parts.ValueKind == JsonValueKind.Null) return null;
        if (parts.ValueKind != JsonValueKind.Array) return InvalidResponse();
        var callCount = 0;
        foreach (var part in parts.EnumerateArray())
        {
            if (AddPart(events, part, toolNames, ref callCount) is { } failure) return failure;
        }
        return null;
    }

    private static ModelFailed? AddPart(
        List<ModelEvent> events, JsonElement part, ProviderToolNameMap toolNames, ref int callCount)
    {
        if (part.ValueKind != JsonValueKind.Object) return InvalidResponse();
        var isThought = false;
        if (part.TryGetProperty("thought", out var thought))
        {
            if (thought.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return InvalidResponse();
            isThought = thought.GetBoolean();
        }
        var hasText = part.TryGetProperty("text", out var text);
        var hasCall = part.TryGetProperty("functionCall", out var call);
        if (hasText && hasCall) return InvalidResponse();
        if (hasText)
        {
            if (text.ValueKind != JsonValueKind.String) return InvalidResponse();
            if (!isThought && text.GetString() is { Length: > 0 } value) events.Add(new TextDelta(value));
        }
        if (!hasCall) return null;
        if (callCount++ >= GeminiToolCallBuffer.MaximumCalls)
            return Failure("tool_calls_limit", "GeminiToolCallsLimit");
        return AddCall(events, call, toolNames);
    }

    private static ModelFailed? AddCall(List<ModelEvent> events, JsonElement call, ProviderToolNameMap toolNames)
    {
        if (call.ValueKind != JsonValueKind.Object || !call.TryGetProperty("name", out var name)
            || name.ValueKind != JsonValueKind.String || name.GetString() is not { } callName
            || string.IsNullOrWhiteSpace(callName) || callName.Length > GeminiToolCallBuffer.MaximumIdentityCharacters)
            return Failure("invalid_tool_arguments", "GeminiInvalidToolCall");
        string? providerCallId = null;
        if (call.TryGetProperty("id", out var identity) && identity.ValueKind != JsonValueKind.Null)
        {
            providerCallId = identity.ValueKind == JsonValueKind.String ? identity.GetString() : null;
            if (string.IsNullOrWhiteSpace(providerCallId)
                || providerCallId.Length > GeminiToolCallBuffer.MaximumIdentityCharacters)
                return InvalidResponse();
        }
        JsonElement arguments;
        if (call.TryGetProperty("args", out var args))
        {
            if (args.ValueKind != JsonValueKind.Object) return Failure("invalid_tool_arguments", "GeminiInvalidToolCall");
            if (args.GetRawText().Length > GeminiToolCallBuffer.MaximumArgumentCharacters)
                return Failure("tool_arguments_limit", "GeminiToolArgumentsLimit");
            arguments = args.Clone();
        }
        else
        {
            using var empty = JsonDocument.Parse("{}");
            arguments = empty.RootElement.Clone();
        }
        events.Add(new ToolCallCompleted(Guid.NewGuid().ToString("N"), toolNames.Decode(callName), arguments, providerCallId));
        return null;
    }

    private static ModelFailed ReadError(JsonElement error)
    {
        if (error.ValueKind != JsonValueKind.Object) return InvalidResponse();
        var code = error.TryGetProperty("code", out var value) ? value.ToString() : "gemini_error";
        var message = error.TryGetProperty("message", out var text) && text.ValueKind == JsonValueKind.String
            ? text.GetString()! : ProviderText.Get("GeminiRequestFailed");
        return new ModelFailed(code, message);
    }

    private static ModelFailed InvalidResponse() => Failure("invalid_response", "GeminiInvalidResponse");
    private static ModelFailed Failure(string code, string resource) => new(code, ProviderText.Get(resource));
}
