using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Providers.Streaming;
using MossAgent.Providers.ToolNames;

namespace MossAgent.Providers.OpenAi;

internal static class OpenAiEventMapper
{
    public static ModelEvent? Map(JsonElement root, ProviderToolNameMap toolNames)
    {
        var type = ReadString(root, "type");
        return type switch
        {
            "response.output_text.delta" => ReadText(root),
            "response.function_call_arguments.done" => ReadToolCall(root, toolNames),
            "response.output_item.done" => ReadCompletedItem(root, toolNames),
            "response.completed" => new ModelCompleted(ReadResponseId(root)),
            "response.failed" => ReadError(root.TryGetProperty("response", out var response)
                && response.ValueKind == JsonValueKind.Object ? response : root),
            "response.incomplete" => new ModelFailed("response_incomplete", ProviderText.Get("ResponseIncomplete")),
            "error" => ReadError(root),
            _ => null
        };
    }

    private static ModelEvent? ReadText(JsonElement root)
    {
        return root.TryGetProperty("delta", out var delta) && delta.ValueKind == JsonValueKind.String
            ? new TextDelta(delta.GetString() ?? string.Empty)
            : null;
    }

    private static ModelEvent? ReadToolCall(
        JsonElement root,
        ProviderToolNameMap toolNames)
    {
        var name = ReadString(root, "name");
        var callId = ReadString(root, "call_id");
        var arguments = ReadString(root, "arguments");
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(callId) || arguments is null)
        {
            return new ModelFailed("invalid_tool_arguments", ProviderText.Get("ResponsesInvalidToolCall"));
        }

        using var document = ProviderFrameJson.TryParse(arguments);
        return document is null
            ? new ModelFailed("invalid_tool_arguments", ProviderText.Get("ResponsesInvalidToolCall"))
            : new ToolCallCompleted(callId, toolNames.Decode(name), document.RootElement.Clone());
    }

    private static ModelEvent? ReadCompletedItem(
        JsonElement root,
        ProviderToolNameMap toolNames)
    {
        if (!root.TryGetProperty("item", out var item)
            || item.ValueKind != JsonValueKind.Object
            || !item.TryGetProperty("type", out var itemType)
            || itemType.ValueKind != JsonValueKind.String
            || itemType.GetString() != "function_call")
        {
            return null;
        }

        return ReadToolCall(item, toolNames);
    }

    private static string? ReadResponseId(JsonElement root)
    {
        return root.TryGetProperty("response", out var response)
            && response.ValueKind == JsonValueKind.Object
                ? ReadString(response, "id")
                : null;
    }

    /// <summary>
    /// Reads a property only when it is a JSON string, so a malformed payload cannot throw
    /// <see cref="InvalidOperationException"/> out of the stream enumeration.
    /// </summary>
    /// <param name="root">Object element that should own the property.</param>
    /// <param name="propertyName">Wire property name.</param>
    private static string? ReadString(JsonElement root, string propertyName) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

    private static ModelEvent ReadError(JsonElement root)
    {
        var error = root.TryGetProperty("error", out var value) && value.ValueKind == JsonValueKind.Object ? value : root;
        var code = error.TryGetProperty("code", out var codeValue)
            && codeValue.ValueKind == JsonValueKind.String ? codeValue.GetString() ?? "openai_error"
            : "openai_error";
        var message = error.TryGetProperty("message", out var messageValue)
            && messageValue.ValueKind == JsonValueKind.String ? messageValue.GetString() ?? ProviderText.Get("ResponseFailed")
            : ProviderText.Get("ResponseFailed");
        return new ModelFailed(code, message);
    }
}
