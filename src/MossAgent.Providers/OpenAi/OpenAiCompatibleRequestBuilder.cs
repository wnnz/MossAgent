using System.Text.Json.Nodes;
using MossAgent.Application.Models;
using MossAgent.Providers.ToolNames;

namespace MossAgent.Providers.OpenAi;

internal static class OpenAiCompatibleRequestBuilder
{
    public static string Build(ModelRequest request, ProviderToolNameMap toolNames)
    {
        var images = new ModelImageRequestValidation(request.Model.SupportsImages);
        var files = new ModelFileRequestValidation(request.Model.SupportsFiles);
        var root = new JsonObject
        {
            ["model"] = request.Model.ModelId,
            ["stream"] = true,
            ["max_tokens"] = request.Model.MaximumOutputTokens,
            ["messages"] = BuildMessages(request, toolNames, ref images, ref files)
        };

        if (request.Tools.Count > 0)
        {
            root["tools"] = BuildTools(request, toolNames);
        }

        if (request.Model.Temperature is { } temperature)
        {
            root["temperature"] = temperature;
        }

        if (request.Model.TopP is { } topP)
        {
            root["top_p"] = topP;
        }

        if (request.Model.ReasoningEffort is not ("off" or "none"))
        {
            root["reasoning_effort"] = request.Model.ReasoningEffort;
        }

        AdvancedParameterMerger.Merge(root, request.Model.AdvancedParametersJson, request.Provider.Protocol);
        return ModelRequestJson.Serialize(root, images.ContainsImages, files.ContainsFiles);
    }

    private static JsonArray BuildMessages(
        ModelRequest request,
        ProviderToolNameMap toolNames,
        ref ModelImageRequestValidation images,
        ref ModelFileRequestValidation files)
    {
        var messages = new JsonArray();
        foreach (var message in request.Messages)
        {
            images.Include(message);
            files.Include(message);
            var item = new JsonObject
            {
                ["role"] = message.Role == ModelRole.ToolObservation ? "user" : message.Role.ToString().ToLowerInvariant(),
                ["content"] = ModelContentBuilder.Compatible(message)
            };

            if (message.Role == ModelRole.Assistant && message.ToolCalls is { Count: > 0 })
            {
                item["tool_calls"] = BuildToolCalls(message.ToolCalls, toolNames);
            }

            if (message.Role == ModelRole.Tool && !string.IsNullOrWhiteSpace(message.ToolCallId))
            {
                item["tool_call_id"] = message.ToolCallId;
            }

            messages.Add(item);
        }

        if (files.ContainsFiles)
            throw new ArgumentException(
                "OpenAI-compatible chat completions does not define a portable PDF input shape.",
                nameof(request));
        return messages;
    }

    private static JsonArray BuildToolCalls(
        IReadOnlyList<ModelToolCall> calls,
        ProviderToolNameMap toolNames)
    {
        var result = new JsonArray();
        foreach (var call in calls)
        {
            result.Add(new JsonObject
            {
                ["id"] = call.CallId,
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = toolNames.Encode(call.Name),
                    ["arguments"] = call.Arguments.GetRawText()
                }
            });
        }

        return result;
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
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = toolNames.Encode(tool.Name),
                    ["description"] = tool.Description,
                    ["parameters"] = JsonNode.Parse(tool.InputSchemaJson)
                }
            });
        }

        return tools;
    }

}
