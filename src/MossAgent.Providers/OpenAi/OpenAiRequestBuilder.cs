using System.Text.Json;
using System.Text.Json.Nodes;
using MossAgent.Application.Models;
using MossAgent.Providers.ToolNames;

namespace MossAgent.Providers.OpenAi;

internal static class OpenAiRequestBuilder
{
    public static string Build(ModelRequest request, ProviderToolNameMap toolNames)
    {
        var root = new JsonObject
        {
            ["model"] = request.Model.ModelId,
            ["stream"] = true,
            ["max_output_tokens"] = request.Model.MaximumOutputTokens,
            ["input"] = BuildMessages(request, toolNames),
            ["tools"] = BuildTools(request, toolNames)
        };

        if (!request.Model.ReasoningEffort.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            root["reasoning"] = new JsonObject { ["effort"] = request.Model.ReasoningEffort };
        }

        if (!string.IsNullOrWhiteSpace(request.PreviousResponseId))
        {
            root["previous_response_id"] = request.PreviousResponseId;
        }

        MergeAdvancedParameters(root, request.Model.AdvancedParametersJson);
        return root.ToJsonString();
    }

    private static JsonArray BuildMessages(
        ModelRequest request,
        ProviderToolNameMap toolNames)
    {
        var messages = new JsonArray();
        foreach (var message in request.Messages)
        {
            if (message.ToolCalls is { Count: > 0 })
            {
                foreach (var call in message.ToolCalls)
                {
                    messages.Add(new JsonObject
                    {
                        ["type"] = "function_call",
                        ["call_id"] = call.CallId,
                        ["name"] = toolNames.Encode(call.Name),
                        ["arguments"] = call.Arguments.GetRawText()
                    });
                }
            }

            if (message.Role == ModelRole.Tool)
            {
                messages.Add(new JsonObject
                {
                    ["type"] = "function_call_output",
                    ["call_id"] = message.ToolCallId,
                    ["output"] = message.Content
                });
            }
            else if (!string.IsNullOrEmpty(message.Content))
            {
                messages.Add(new JsonObject
                {
                    ["role"] = message.Role.ToString().ToLowerInvariant(),
                    ["content"] = message.Content
                });
            }
        }

        return messages;
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
                ["name"] = toolNames.Encode(tool.Name),
                ["description"] = tool.Description,
                ["parameters"] = JsonNode.Parse(tool.InputSchemaJson),
                ["strict"] = false
            });
        }

        return tools;
    }

    private static void MergeAdvancedParameters(JsonObject root, string json)
    {
        if (string.IsNullOrWhiteSpace(json) || JsonNode.Parse(json) is not JsonObject advanced)
        {
            return;
        }

        string[] protectedNames = ["model", "stream", "input", "tools"];
        foreach (var pair in advanced)
        {
            if (!protectedNames.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                root[pair.Key] = pair.Value?.DeepClone();
            }
        }
    }
}
