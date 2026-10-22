using System.Text.Json;
using System.Text.Json.Nodes;
using MossAgent.Application.Models;
using MossAgent.Providers.ToolNames;

namespace MossAgent.Providers.Gemini;

internal static class GeminiRequestBuilder
{
    public static string Build(ModelRequest request, ProviderToolNameMap toolNames)
    {
        var images = new ModelImageRequestValidation(request.Model.SupportsImages);
        var files = new ModelFileRequestValidation(request.Model.SupportsFiles);
        if (!ModelReasoningPolicy.Supports(request.Provider.Protocol, request.Model.ModelId, request.Model.ReasoningEffort))
            throw new ArgumentException("Unsupported Gemini reasoning effort.", nameof(request));
        var root = new JsonObject
        {
            ["contents"] = BuildContents(request, toolNames, ref images, ref files),
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

        AdvancedParameterMerger.Merge(root, request.Model.AdvancedParametersJson, request.Provider.Protocol);
        return ModelRequestJson.Serialize(root, images.ContainsImages, files.ContainsFiles);
    }

    private static JsonArray BuildContents(
        ModelRequest request,
        ProviderToolNameMap toolNames,
        ref ModelImageRequestValidation images,
        ref ModelFileRequestValidation files)
    {
        var result = new JsonArray();
        JsonArray? previousToolParts = null;
        ILookup<string, ModelToolCall>? toolCalls = null;
        foreach (var message in request.Messages)
        {
            images.Include(message);
            files.Include(message);
            if (message.Role == ModelRole.System) continue;
            JsonArray parts;
            if (message.Role == ModelRole.Tool)
            {
                toolCalls ??= request.Messages.Where(static item => item.Role == ModelRole.Assistant)
                    .SelectMany(static item => item.ToolCalls ?? [])
                    .ToLookup(static call => call.CallId, StringComparer.Ordinal);
                var part = BuildToolResponsePart(message, toolNames,
                    toolCalls[message.ToolCallId!].FirstOrDefault()?.ProviderCallId);
                if (previousToolParts is not null)
                {
                    previousToolParts.Add(part);
                    continue;
                }
                parts = new JsonArray(part);
            }
            else parts = BuildParts(request, message, toolNames);
            result.Add(new JsonObject
            {
                ["role"] = message.Role == ModelRole.Assistant ? "model" : "user",
                ["parts"] = parts
            });
            previousToolParts = message.Role == ModelRole.Tool ? parts : null;
        }

        return result;
    }

    private static JsonObject BuildToolResponsePart(
        ModelMessage message, ProviderToolNameMap toolNames, string? nativeId)
    {
        var response = new JsonObject
        {
            ["name"] = toolNames.Encode(message.ToolName ?? string.Empty),
            ["response"] = BuildToolResponse(message.Content)
        };
        if (nativeId is not null) response["id"] = nativeId;
        return new JsonObject { ["functionResponse"] = response };
    }

    private static JsonArray BuildParts(
        ModelRequest request,
        ModelMessage message,
        ProviderToolNameMap toolNames)
    {
        if (message.Role == ModelRole.Assistant && message.Continuation is { } continuation)
            return GeminiContinuationParts.Restore(continuation, request);
        var parts = new JsonArray();

        if (!string.IsNullOrWhiteSpace(message.Content))
        {
            parts.Add(new JsonObject { ["text"] = message.Content });
        }

        ModelContentBuilder.AppendGemini(parts, message);
        foreach (var call in message.Role == ModelRole.Assistant ? message.ToolCalls ?? [] : [])
        {
            var function = new JsonObject
            {
                ["name"] = toolNames.Encode(call.Name),
                ["args"] = JsonNode.Parse(call.Arguments.GetRawText())
            };
            if (call.ProviderCallId is not null) function["id"] = call.ProviderCallId;
            parts.Add(new JsonObject
            {
                ["functionCall"] = function
            });
        }

        return parts;
    }

    private static JsonObject BuildToolResponse(string content)
    {
        try
        {
            if (JsonNode.Parse(content) is JsonObject response) return response;
        }
        catch (JsonException) { }
        return new JsonObject { ["output"] = content };
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

        var thinking = BuildThinkingConfig(
            request.Model.ModelId,
            request.Model.ReasoningEffort,
            request.Model.MaximumOutputTokens);
        if (thinking is not null)
        {
            config["thinkingConfig"] = thinking;
        }

        return config;
    }

    private static JsonObject? BuildThinkingConfig(
        string modelId,
        string reasoningEffort,
        int maximumOutputTokens)
    {
        if (modelId.StartsWith("gemini-3", StringComparison.OrdinalIgnoreCase)
            || !modelId.StartsWith("gemini-2.5", StringComparison.OrdinalIgnoreCase)
            && ModelCapabilityCatalog.Find(MossAgent.Domain.ProviderProtocol.GeminiGenerateContent, modelId)
                is { ReasoningEfforts.IsEmpty: false })
        {
            var level = MapThinkingLevel(reasoningEffort);
            return level is null ? null : new JsonObject { ["thinkingLevel"] = level };
        }

        if (!modelId.StartsWith("gemini-2.5", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var budget = MapGemini25ThinkingBudget(modelId, reasoningEffort, maximumOutputTokens);
        return budget is null ? null : new JsonObject { ["thinkingBudget"] = budget.Value };
    }

    private static int? MapGemini25ThinkingBudget(
        string modelId,
        string reasoningEffort,
        int maximumOutputTokens)
    {
        var isPro = modelId.Contains("-pro", StringComparison.OrdinalIgnoreCase);
        var maximumBudget = isPro ? 32_768 : 24_576;
        int? requestedBudget = ModelReasoningPolicy.Normalize(reasoningEffort) switch
        {
            "none" or "off" => isPro ? null : 0,
            "minimal" => isPro ? 128 : 0,
            "low" => 1_024,
            "medium" => 4_096,
            "high" => 8_192,
            "xhigh" or "max" => maximumBudget,
            _ => null
        };
        if (requestedBudget is null or 0)
        {
            return requestedBudget;
        }

        var responseReserve = Math.Min(1_024, Math.Max(0, maximumOutputTokens / 2));
        var availableBudget = Math.Max(0, maximumOutputTokens - responseReserve);
        var budget = Math.Min(Math.Min(requestedBudget.Value, maximumBudget), availableBudget);
        return isPro && budget < 128 ? null : budget;
    }

    private static string? MapThinkingLevel(string effort) =>
        ModelReasoningPolicy.Normalize(effort) switch
        {
            "minimal" => "MINIMAL",
            "low" => "LOW",
            "medium" => "MEDIUM",
            "high" => "HIGH",
            _ => null
        };

}
