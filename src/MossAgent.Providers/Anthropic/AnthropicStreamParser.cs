using System.Text;
using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Providers.Streaming;
using MossAgent.Providers.ToolNames;

namespace MossAgent.Providers.Anthropic;

internal sealed class AnthropicStreamParser(ProviderToolNameMap toolNames)
{
    private readonly Dictionary<int, (string CallId, string WireName, StringBuilder Arguments)> _calls = [];

    public ModelEvent? Parse(JsonElement root)
    {
        var type = ReadString(root, "type");
        return type switch
        {
            "content_block_start" => StartBlock(root),
            "content_block_delta" => ReadDelta(root),
            "content_block_stop" => StopBlock(root),
            "message_stop" => new ModelCompleted(),
            "error" => ReadError(root),
            _ => null
        };
    }

    private ModelEvent? StartBlock(JsonElement root)
    {
        if (!TryGetIndex(root, out var index) || !root.TryGetProperty("content_block", out var block)
            || block.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var type = ReadString(block, "type");
        if (type == "tool_use")
        {
            var callId = ReadString(block, "id");
            var wireName = ReadString(block, "name");
            if (string.IsNullOrWhiteSpace(callId) || string.IsNullOrWhiteSpace(wireName))
                return InvalidToolCall();
            _calls[index] = (callId, wireName, new StringBuilder());
        }

        return type == "text" && block.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String
            ? new TextDelta(text.GetString() ?? string.Empty)
            : null;
    }

    private ModelEvent? ReadDelta(JsonElement root)
    {
        if (!TryGetIndex(root, out var index) || !root.TryGetProperty("delta", out var delta)
            || delta.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var type = ReadString(delta, "type");
        if (type == "input_json_delta")
        {
            if (!_calls.TryGetValue(index, out var call)) return null;
            var partial = ReadString(delta, "partial_json");
            if (partial is null) return InvalidToolCall();
            call.Arguments.Append(partial);
            return null;
        }

        return type == "text_delta" && delta.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String
            ? new TextDelta(text.GetString() ?? string.Empty)
            : null;
    }

    private ModelEvent? StopBlock(JsonElement root)
    {
        if (!TryGetIndex(root, out var index) || !_calls.Remove(index, out var call))
        {
            return null;
        }

        using var document = ProviderFrameJson.TryParse(call.Arguments.Length == 0 ? "{}" : call.Arguments.ToString());
        return document is null
            ? InvalidToolCall()
            : new ToolCallCompleted(call.CallId, toolNames.Decode(call.WireName), document.RootElement.Clone());
    }

    /// <summary>
    /// Reports an incomplete or unparseable tool call as a terminal failure so no partial tool call
    /// reaches the execution pipeline.
    /// </summary>
    private static ModelFailed InvalidToolCall() =>
        new("invalid_tool_arguments", ProviderText.Get("AnthropicInvalidToolCall"));

    private static ModelEvent ReadError(JsonElement root)
    {
        var error = root.TryGetProperty("error", out var value) && value.ValueKind == JsonValueKind.Object ? value : root;
        var type = ReadString(error, "type") ?? "anthropic_error";
        var message = ReadString(error, "message") ?? ProviderText.Get("AnthropicRequestFailed");
        return new ModelFailed(type, message);
    }

    private static bool TryGetIndex(JsonElement root, out int index)
    {
        index = 0;
        return root.TryGetProperty("index", out var value) && value.TryGetInt32(out index);
    }

    /// <summary>
    /// Reads a property only when it is a JSON string, so a malformed payload cannot throw
    /// out of the stream enumeration.
    /// </summary>
    /// <param name="root">Object element that should own the property.</param>
    /// <param name="propertyName">Wire property name.</param>
    private static string? ReadString(JsonElement root, string propertyName) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
}
