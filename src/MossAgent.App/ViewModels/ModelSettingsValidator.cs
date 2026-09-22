using System.Text.Json;

namespace MossAgent.App.ViewModels;

internal static class ModelSettingsValidator
{
    public static string? Validate(
        bool hasProvider,
        string modelId,
        double? temperature,
        double? topP,
        string advancedJson)
    {
        if (!hasProvider || string.IsNullOrWhiteSpace(modelId))
        {
            return "请选择供应商并填写模型 ID。";
        }

        if (temperature is < 0 or > 2 || topP is < 0 or > 1)
        {
            return "Temperature 必须在 0–2，Top-P 必须在 0–1。";
        }

        try
        {
            using var document = JsonDocument.Parse(advancedJson);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? null
                : "高级参数必须是 JSON 对象。";
        }
        catch (JsonException)
        {
            return "高级参数必须是 JSON 对象。";
        }
    }
}
