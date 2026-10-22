using System.Text.Json;
using System.Text.Json.Nodes;
using MossAgent.Application.Models;
using MossAgent.Providers.ToolNames;

namespace MossAgent.Providers.OpenAi;

internal static class OpenAiRequestBuilder
{
    public static string Build(ModelRequest request, ProviderToolNameMap toolNames)
    {
        var images = new ModelImageRequestValidation(request.Model.SupportsImages);
        var files = new ModelFileRequestValidation(request.Model.SupportsFiles);
        var root = new JsonObject
        {
            ["model"] = request.Model.ModelId,
            ["stream"] = true,
            ["max_output_tokens"] = request.Model.MaximumOutputTokens,
            ["input"] = BuildMessages(request, toolNames, ref images, ref files),
            ["tools"] = BuildTools(request, toolNames)
        };

        if (!request.Model.ReasoningEffort.Equals("off", StringComparison.OrdinalIgnoreCase)
            && (ModelReasoningPolicy.Normalize(request.Model.ReasoningEffort) != "none"
                || ModelCapabilityCatalog.Find(request.Provider.Protocol, request.Model.ModelId) is not { ReasoningEfforts.IsEmpty: true }))
        {
            root["reasoning"] = new JsonObject { ["effort"] = request.Model.ReasoningEffort };
        }

        if (request.Model.Temperature is { } temperature)
            root["temperature"] = temperature;
        if (request.Model.TopP is { } topP)
            root["top_p"] = topP;

        if (!string.IsNullOrWhiteSpace(request.PreviousResponseId))
        {
            root["previous_response_id"] = request.PreviousResponseId;
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
            if (message.Role == ModelRole.Assistant && message.ToolCalls is { Count: > 0 })
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
            else if (!string.IsNullOrEmpty(message.Content) || !message.Images.IsDefaultOrEmpty
                || !message.Files.IsDefaultOrEmpty)
            {
                messages.Add(new JsonObject
                {
                    ["role"] = message.Role == ModelRole.ToolObservation ? "user" : message.Role.ToString().ToLowerInvariant(),
                    ["content"] = ModelContentBuilder.Responses(message)
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

}
