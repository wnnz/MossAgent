using System.Text;
using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Providers.ToolNames;

namespace MossAgent.Providers.Anthropic;

internal sealed class AnthropicStreamParser(ProviderToolNameMap toolNames)
{
    private readonly Dictionary<int, string> _callIds = [];
    private readonly Dictionary<int, string> _toolNames = [];
    private readonly Dictionary<int, StringBuilder> _arguments = [];

    public ModelEvent? Parse(JsonElement root)
    {
        var type = root.TryGetProperty("type", out var value) ? value.GetString() : null;
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
        if (!TryGetIndex(root, out var index)
            || !root.TryGetProperty("content_block", out var block))
        {
            return null;
        }

        var type = block.TryGetProperty("type", out var typeValue) ? typeValue.GetString() : null;
        if (type == "tool_use")
        {
            _callIds[index] = block.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N");
            _toolNames[index] = block.GetProperty("name").GetString() ?? string.Empty;
            _arguments[index] = new StringBuilder();
        }

        return type == "text" && block.TryGetProperty("text", out var text)
            ? new TextDelta(text.GetString() ?? string.Empty)
            : null;
    }

    private ModelEvent? ReadDelta(JsonElement root)
    {
        if (!TryGetIndex(root, out var index) || !root.TryGetProperty("delta", out var delta))
        {
            return null;
        }

        var type = delta.TryGetProperty("type", out var typeValue) ? typeValue.GetString() : null;
        if (type == "input_json_delta" && _arguments.TryGetValue(index, out var arguments))
        {
            arguments.Append(delta.GetProperty("partial_json").GetString());
            return null;
        }

        return type == "text_delta" && delta.TryGetProperty("text", out var text)
            ? new TextDelta(text.GetString() ?? string.Empty)
            : null;
    }

    private ModelEvent? StopBlock(JsonElement root)
    {
        if (!TryGetIndex(root, out var index)
            || !_callIds.Remove(index, out var callId)
            || !_toolNames.Remove(index, out var name)
            || !_arguments.Remove(index, out var arguments))
        {
            return null;
        }

        using var document = JsonDocument.Parse(arguments.Length == 0 ? "{}" : arguments.ToString());
        return new ToolCallCompleted(
            callId, toolNames.Decode(name), document.RootElement.Clone());
    }

    private static ModelEvent ReadError(JsonElement root)
    {
        var error = root.TryGetProperty("error", out var value) ? value : root;
        var type = error.TryGetProperty("type", out var typeValue)
            ? typeValue.GetString() ?? "anthropic_error"
            : "anthropic_error";
        var message = error.TryGetProperty("message", out var messageValue)
            ? messageValue.GetString() ?? "Anthropic 请求失败。"
            : "Anthropic 请求失败。";
        return new ModelFailed(type, message);
    }

    private static bool TryGetIndex(JsonElement root, out int index)
    {
        index = 0;
        return root.TryGetProperty("index", out var value) && value.TryGetInt32(out index);
    }
}
