using System.Text.Json.Nodes;
using MossAgent.Application.Models;
using MossAgent.Providers.ToolNames;

namespace MossAgent.Providers.OpenAi;

internal static class OpenAiCompatibleRequestBuilder
{
    public static string Build(ModelRequest request, ProviderToolNameMap toolNames)
    {
        var images = ModelImageRequestValidation.For(request);
        if (!ModelReasoningPolicy.Supports(request.Provider.Protocol,
                request.Model.ModelId, request.Model.ReasoningEffort))
        {
            throw new ModelCapabilityException(ModelCapabilityException.UnsupportedReasoningEffort,
                "The configured reasoning effort is not a valid effort token for this protocol. "
                + "Fix it in model settings.");
        }
        var root = new JsonObject
        {
            ["model"] = request.Model.ModelId,
            ["stream"] = true,
            ["max_tokens"] = request.Model.MaximumOutputTokens,
            ["messages"] = BuildMessages(request, toolNames, ref images)
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

        AdvancedParameterMerger.Merge(root, request.Model.AdvancedParametersJson,
            request.Provider.Protocol, request.Model.MaximumOutputTokens);
        return ModelRequestJson.Serialize(root, images.ContainsImages);
    }

    private static JsonArray BuildMessages(
        ModelRequest request,
        ProviderToolNameMap toolNames,
        ref ModelImageRequestValidation images)
    {
        var messages = new JsonArray();
        foreach (var message in request.Messages)
        {
            images.Include(message);
            var item = new JsonObject
            {
                ["role"] = message.Role == ModelRole.ToolObservation ? "user" : message.Role.ToString().ToLowerInvariant(),
                ["content"] = ModelImageContentBuilder.Compatible(message)
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
