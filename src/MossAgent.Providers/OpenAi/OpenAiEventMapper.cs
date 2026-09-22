using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Providers.ToolNames;

namespace MossAgent.Providers.OpenAi;

internal static class OpenAiEventMapper
{
    public static ModelEvent? Map(JsonElement root, ProviderToolNameMap toolNames)
    {
        var type = root.TryGetProperty("type", out var typeProperty)
            ? typeProperty.GetString()
            : null;
        return type switch
        {
            "response.output_text.delta" => ReadText(root),
            "response.function_call_arguments.done" => ReadToolCall(root, toolNames),
            "response.output_item.done" => ReadCompletedItem(root, toolNames),
            "response.completed" => new ModelCompleted(ReadResponseId(root)),
            "error" => ReadError(root),
            _ => null
        };
    }

    private static ModelEvent? ReadText(JsonElement root)
    {
        return root.TryGetProperty("delta", out var delta)
            ? new TextDelta(delta.GetString() ?? string.Empty)
            : null;
    }

    private static ModelEvent? ReadToolCall(
        JsonElement root,
        ProviderToolNameMap toolNames)
    {
        if (!root.TryGetProperty("name", out var name)
            || !root.TryGetProperty("call_id", out var callId)
            || !root.TryGetProperty("arguments", out var arguments))
        {
            return null;
        }

        using var document = JsonDocument.Parse(arguments.GetString() ?? "{}");
        return new ToolCallCompleted(
            callId.GetString() ?? Guid.NewGuid().ToString("N"),
            toolNames.Decode(name.GetString() ?? string.Empty),
            document.RootElement.Clone());
    }

    private static ModelEvent? ReadCompletedItem(
        JsonElement root,
        ProviderToolNameMap toolNames)
    {
        if (!root.TryGetProperty("item", out var item)
            || !item.TryGetProperty("type", out var itemType)
            || itemType.GetString() != "function_call")
        {
            return null;
        }

        return ReadToolCall(item, toolNames);
    }

    private static string? ReadResponseId(JsonElement root)
    {
        return root.TryGetProperty("response", out var response)
            && response.TryGetProperty("id", out var id)
                ? id.GetString()
                : null;
    }

    private static ModelEvent ReadError(JsonElement root)
    {
        var error = root.TryGetProperty("error", out var value) ? value : root;
        var code = error.TryGetProperty("code", out var codeValue)
            ? codeValue.GetString() ?? "openai_error"
            : "openai_error";
        var message = error.TryGetProperty("message", out var messageValue)
            ? messageValue.GetString() ?? "OpenAI 请求失败。"
            : "OpenAI 请求失败。";
        return new ModelFailed(code, message);
    }
}
