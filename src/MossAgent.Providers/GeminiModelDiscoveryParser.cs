using System.Text.Json;
using MossAgent.Application.Models;

namespace MossAgent.Providers;

internal static class GeminiModelDiscoveryParser
{
    public static IReadOnlyList<DiscoveredModel> ReadModels(JsonElement root)
    {
        if (!root.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return models.EnumerateArray()
            .Select(ReadModel)
            .Where(static model => model is not null)
            .Cast<DiscoveredModel>()
            .OrderBy(static model => model.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string? ReadNextPageToken(JsonElement root)
    {
        if (!root.TryGetProperty("nextPageToken", out var token) || token.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = token.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static DiscoveredModel? ReadModel(JsonElement item)
    {
        var name = item.TryGetProperty("name", out var nameProperty) ? nameProperty.GetString() : null;
        if (string.IsNullOrWhiteSpace(name) || !SupportsGeneration(item)) return null;

        var modelId = name.StartsWith("models/", StringComparison.Ordinal) ? name[7..] : name;
        var display = item.TryGetProperty("displayName", out var displayProperty) ? displayProperty.GetString() : modelId;
        return new DiscoveredModel(modelId, string.IsNullOrWhiteSpace(display) ? modelId : display);
    }

    private static bool SupportsGeneration(JsonElement item)
    {
        if (!item.TryGetProperty("supportedGenerationMethods", out var methods)) return true;
        if (methods.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Provider returned invalid model generation methods.");
        }

        var supported = false;
        foreach (var method in methods.EnumerateArray())
        {
            if (method.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException("Provider returned invalid model generation methods.");
            }
            if (method.GetString() is "generateContent" or "streamGenerateContent") supported = true;
        }
        return supported;
    }
}
