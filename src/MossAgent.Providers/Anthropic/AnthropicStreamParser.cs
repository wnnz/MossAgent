using System.Text;
using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Providers.ToolNames;

namespace MossAgent.Providers.Anthropic;

internal sealed class AnthropicStreamParser(ProviderToolNameMap toolNames)
{
    private readonly Dictionary<int, (string CallId, string WireName, StringBuilder Arguments)> _calls = [];

    public ModelEvent? Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return new ModelFailed("invalid_response", ProviderText.Get("StreamInvalidResponse"));
        }

        var type = ReadString(root, "type");
        return type switch
        {
            "content_block_start" => StartBlock(root),
            "content_block_delta" => ReadDelta(root),
            "content_block_stop" => StopBlock(root),
            "message_delta" => ReadMessageDelta(root),
            "message_stop" => StopMessage(),
            "error" => ReadError(root),
            _ => null
        };
    }

    private ModelEvent? StartBlock(JsonElement root)
    {
        if (!TryGetIndex(root, out var index)
            || !root.TryGetProperty("content_block", out var block)
            || block.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var type = ReadString(block, "type");
        if (type == "tool_use")
        {
            var id = ReadString(block, "id");
            var name = ReadString(block, "name");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
            {
                return new ModelFailed("invalid_tool_arguments", ProviderText.Get("StreamInvalidToolCall"));
            }

            _calls[index] = (id, name, new StringBuilder());
        }

        return type == "text" && block.TryGetProperty("text", out var text)
            && text.ValueKind == JsonValueKind.String
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
        if (type == "input_json_delta" && _calls.TryGetValue(index, out var call))
        {
            if (delta.TryGetProperty("partial_json", out var partial)
                && partial.ValueKind == JsonValueKind.String)
            {
                call.Arguments.Append(partial.GetString());
            }

            return null;
        }

        return type == "text_delta" && delta.TryGetProperty("text", out var text)
            && text.ValueKind == JsonValueKind.String
            ? new TextDelta(text.GetString() ?? string.Empty)
            : null;
    }

    private ModelEvent? StopBlock(JsonElement root)
    {
        if (!TryGetIndex(root, out var index) || !_calls.Remove(index, out var call))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(call.Arguments.Length == 0 ? "{}" : call.Arguments.ToString());
            return new ToolCallCompleted(
                call.CallId, toolNames.Decode(call.WireName), document.RootElement.Clone());
        }
        catch (JsonException)
        {
            return new ModelFailed("invalid_tool_arguments", ProviderText.Get("StreamInvalidToolCall"));
        }
    }

    private ModelEvent? StopMessage() => _calls.Count == 0
        ? new ModelCompleted()
        : new ModelFailed("invalid_response", ProviderText.Get("StreamInvalidResponse"));

    private static ModelEvent? ReadMessageDelta(JsonElement root)
    {
        if (!root.TryGetProperty("delta", out var delta) || delta.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return ReadString(delta, "stop_reason") switch
        {
            "max_tokens" => new ModelFailed("output_limit", ProviderText.Get("StreamOutputLimit")),
            "refusal" => new ModelFailed("provider_blocked", ProviderText.Get("StreamProviderBlocked")),
            _ => null
        };
    }

    private static ModelEvent ReadError(JsonElement root)
    {
        var error = root.TryGetProperty("error", out var value) ? value : root;
        var type = ReadString(error, "type") ?? "anthropic_error";
        var message = ReadString(error, "message") ?? "Anthropic 请求失败。";
        return new ModelFailed(type, message);
    }

    private static string? ReadString(JsonElement root, string propertyName) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool TryGetIndex(JsonElement root, out int index)
    {
        index = 0;
        return root.TryGetProperty("index", out var value) && value.TryGetInt32(out index);
    }
}
