using System.Text.Json.Nodes;
using MossAgent.Application.Models;
using MossAgent.Providers.ToolNames;

namespace MossAgent.Providers.Gemini;

internal static class GeminiRequestBuilder
{
    public static string Build(ModelRequest request, ProviderToolNameMap toolNames)
    {
        var root = new JsonObject
        {
            ["contents"] = BuildContents(request, toolNames),
            ["tools"] = BuildTools(request, toolNames),
            ["generationConfig"] = BuildGenerationConfig(request)
        };
        var system = string.Join("\n\n", request.Messages
            .Where(static message => message.Role == ModelRole.System)
            .Select(static message => message.Content));
        if (!string.IsNullOrWhiteSpace(system))
        {
            root["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray(new JsonObject { ["text"] = system })
            };
        }

        MergeAdvancedParameters(root, request.Model.AdvancedParametersJson);
        return root.ToJsonString();
    }

    private static JsonArray BuildContents(
        ModelRequest request,
        ProviderToolNameMap toolNames)
    {
        var result = new JsonArray();
        foreach (var message in request.Messages.Where(static item => item.Role != ModelRole.System))
        {
            result.Add(new JsonObject
            {
                ["role"] = message.Role == ModelRole.Assistant ? "model" : "user",
                ["parts"] = BuildParts(message, toolNames)
            });
        }

        return result;
    }

    private static JsonArray BuildParts(
        ModelMessage message,
        ProviderToolNameMap toolNames)
    {
        var parts = new JsonArray();
        if (message.Role == ModelRole.Tool)
        {
            parts.Add(new JsonObject
            {
                ["functionResponse"] = new JsonObject
                {
                    ["name"] = toolNames.Encode(message.ToolName ?? string.Empty),
                    ["response"] = JsonNode.Parse(message.Content)
                }
            });
            return parts;
        }

        if (!string.IsNullOrWhiteSpace(message.Content))
        {
            parts.Add(new JsonObject { ["text"] = message.Content });
        }

        foreach (var call in message.ToolCalls ?? [])
        {
            parts.Add(new JsonObject
            {
                ["functionCall"] = new JsonObject
                {
                    ["name"] = toolNames.Encode(call.Name),
                    ["args"] = JsonNode.Parse(call.Arguments.GetRawText())
                }
            });
        }

        return parts;
    }

    private static JsonArray BuildTools(
        ModelRequest request,
        ProviderToolNameMap toolNames)
    {
        var declarations = new JsonArray();
        foreach (var tool in request.Tools)
        {
            declarations.Add(new JsonObject
            {
                ["name"] = toolNames.Encode(tool.Name),
                ["description"] = tool.Description,
                ["parameters"] = JsonNode.Parse(tool.InputSchemaJson)
            });
        }

        return new JsonArray(new JsonObject { ["functionDeclarations"] = declarations });
    }

    private static JsonObject BuildGenerationConfig(ModelRequest request)
    {
        var config = new JsonObject { ["maxOutputTokens"] = request.Model.MaximumOutputTokens };
        if (request.Model.Temperature is { } temperature)
        {
            config["temperature"] = temperature;
        }

        if (request.Model.TopP is { } topP)
        {
            config["topP"] = topP;
        }

        return config;
    }

    private static void MergeAdvancedParameters(JsonObject root, string json)
    {
        if (string.IsNullOrWhiteSpace(json) || JsonNode.Parse(json) is not JsonObject advanced)
        {
            return;
        }

        string[] protectedNames = ["contents", "tools"];
        foreach (var pair in advanced)
        {
            if (!protectedNames.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                root[pair.Key] = pair.Value?.DeepClone();
            }
        }
    }
}
