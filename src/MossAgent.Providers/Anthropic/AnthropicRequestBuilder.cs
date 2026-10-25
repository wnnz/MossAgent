using System.Text.Json.Nodes;
using MossAgent.Application.Models;
using MossAgent.Providers.ToolNames;

namespace MossAgent.Providers.Anthropic;

internal static class AnthropicRequestBuilder
{
    public static string Build(ModelRequest request, ProviderToolNameMap toolNames)
    {
        var images = ModelImageRequestValidation.For(request);
        if (!ModelReasoningPolicy.Supports(request.Provider.Protocol,
                request.Model.ModelId, request.Model.ReasoningEffort))
        {
            throw new ModelCapabilityException(ModelCapabilityException.UnsupportedReasoningEffort,
                "The selected model does not support the configured reasoning effort. "
                + "Choose a supported effort in model settings.");
        }
        var root = new JsonObject
        {
            ["model"] = request.Model.ModelId,
            ["max_tokens"] = request.Model.MaximumOutputTokens,
            ["stream"] = true,
            ["messages"] = BuildMessages(request, toolNames, ref images),
            ["tools"] = BuildTools(request, toolNames)
        };
        var system = string.Join("\n\n", request.Messages
            .Where(static message => message.Role == ModelRole.System)
            .Select(static message => message.Content));
        if (!string.IsNullOrWhiteSpace(system))
        {
            root["system"] = system;
        }

        if (request.Model.Temperature is { } temperature)
        {
            root["temperature"] = temperature;
        }

        if (request.Model.TopP is { } topP)
            root["top_p"] = topP;

        AnthropicThinkingOptions.Apply(
            root,
            request.Model.ModelId,
            request.Model.ReasoningEffort,
            request.Model.MaximumOutputTokens);

        AdvancedParameterMerger.Merge(root, request.Model.AdvancedParametersJson,
            request.Provider.Protocol, request.Model.MaximumOutputTokens);
        return ModelRequestJson.Serialize(root, images.ContainsImages);
    }

    private static JsonArray BuildMessages(
        ModelRequest request,
        ProviderToolNameMap toolNames,
        ref ModelImageRequestValidation images)
    {
        var result = new JsonArray();
        foreach (var message in request.Messages)
        {
            images.Include(message);
            if (message.Role == ModelRole.System) continue;
            var content = BuildContent(message, toolNames);
            result.Add(new JsonObject
            {
                ["role"] = message.Role == ModelRole.Assistant ? "assistant" : "user",
                ["content"] = content
            });
        }

        return result;
    }

    private static JsonNode BuildContent(
        ModelMessage message,
        ProviderToolNameMap toolNames)
    {
        if (message.Role == ModelRole.Tool)
        {
            return new JsonArray(new JsonObject
            {
                ["type"] = "tool_result",
                ["tool_use_id"] = message.ToolCallId,
                ["content"] = message.Content
            });
        }

        if (message.Role != ModelRole.Assistant || message.ToolCalls is not { Count: > 0 })
        {
            return ModelImageContentBuilder.Anthropic(message);
        }

        var blocks = new JsonArray();
        if (!string.IsNullOrWhiteSpace(message.Content))
        {
            blocks.Add(new JsonObject { ["type"] = "text", ["text"] = message.Content });
        }

        foreach (var call in message.ToolCalls)
        {
            blocks.Add(new JsonObject
            {
                ["type"] = "tool_use",
                ["id"] = call.CallId,
                ["name"] = toolNames.Encode(call.Name),
                ["input"] = JsonNode.Parse(call.Arguments.GetRawText())
            });
        }

        return blocks;
    }

    private static JsonArray BuildTools(
        ModelRequest request,
        ProviderToolNameMap toolNames)
    {
        var tools = new JsonArray();
        foreach (var tool in request.Tools)
        {
            tools.Add(new JsonObject
            {
                ["name"] = toolNames.Encode(tool.Name),
                ["description"] = tool.Description,
                ["input_schema"] = JsonNode.Parse(tool.InputSchemaJson)
            });
        }

        return tools;
    }

}
