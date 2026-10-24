using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Domain;

using MossAgent.App.Resources;
namespace MossAgent.App.ViewModels;

internal static class ModelSettingsValidator
{
    private const int MinimumContextLength = 1_024;
    private const int MaximumContextLength = 4_000_000;
    private const int MinimumOutputTokens = 256;
    private const int MaximumOutputTokens = 1_000_000;

    public static string? Validate(
        ProviderProtocol? protocol,
        string modelId,
        int contextLength,
        int maximumOutputTokens,
        string reasoningEffort,
        double? temperature,
        double? topP,
        string advancedJson)
    {
        if (protocol is null || string.IsNullOrWhiteSpace(modelId))
        {
            return UiText.T("请选择供应商并填写模型 ID。");
        }

        return ValidateContextLength(contextLength)
            ?? ValidateMaximumOutputTokens(contextLength, maximumOutputTokens)
            ?? ValidateReasoningEffort(protocol.Value, modelId, reasoningEffort)
            ?? ValidateSamplingParameters(temperature, topP)
            ?? ValidateAdvancedParameters(advancedJson, protocol.Value, maximumOutputTokens);
    }

    private static string? ValidateContextLength(int contextLength) =>
        contextLength is < MinimumContextLength or > MaximumContextLength
            ? UiText.T("上下文长度必须在 1,024–4,000,000 之间。")
            : null;

    private static string? ValidateMaximumOutputTokens(int contextLength, int maximumOutputTokens)
    {
        if (maximumOutputTokens is < MinimumOutputTokens or > MaximumOutputTokens)
        {
            return UiText.T("最大输出 token 数必须在 256–1,000,000 之间。");
        }

        return maximumOutputTokens >= contextLength
            ? UiText.T("最大输出 token 数必须小于上下文长度。")
            : null;
    }

    private static string? ValidateReasoningEffort(ProviderProtocol protocol, string modelId, string? reasoningEffort) =>
        ModelReasoningPolicy.Supports(protocol, modelId, reasoningEffort)
            ? null : MossAgent.App.Resources.ProviderSettingsText.Get("UnsupportedReasoningEffort");

    private static string? ValidateSamplingParameters(double? temperature, double? topP) =>
        temperature is < 0 or > 2 || topP is < 0 or > 1
            ? UiText.T("Temperature 必须在 0–2，Top-P 必须在 0–1。")
            : null;

    private static string? ValidateAdvancedParameters(
        string? advancedJson, ProviderProtocol protocol, int maximumOutputTokens)
    {
        try
        {
            using var document = JsonDocument.Parse(advancedJson ?? string.Empty);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return UiText.T("高级参数必须是 JSON 对象。");

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (protocol == ProviderProtocol.OpenAiCompatible
                    && property.Name.Equals("max_completion_tokens", StringComparison.OrdinalIgnoreCase))
                {
                    if (property.Value.ValueKind != JsonValueKind.Number
                        || !property.Value.TryGetInt32(out var alternateLimit)
                        || alternateLimit is < 1 || alternateLimit > maximumOutputTokens)
                        return UiText.T("max_completion_tokens 必须为不超过最大输出 Token 的正整数。");
                    continue;
                }

                if (ModelParameterPolicy.IsManagedRoot(protocol, property.Name))
                    return UiText.T("高级参数不能覆盖由界面管理的模型、消息、工具或输出设置。");

                var objectName = ModelParameterPolicy.MergeableObjectName(protocol, property.Name);
                if (objectName is null) continue;
                if (property.Value.ValueKind != JsonValueKind.Object)
                    return UiText.T("高级参数中的嵌套配置必须是 JSON 对象。");
                if (property.Value.EnumerateObject().Any(nested =>
                    ModelParameterPolicy.IsManagedNested(protocol, objectName, nested.Name)))
                    return UiText.T("高级参数不能覆盖由界面管理的模型、消息、工具或输出设置。");
            }

            return null;
        }
        catch (JsonException)
        {
            return UiText.T("高级参数必须是 JSON 对象。");
        }
    }
}

