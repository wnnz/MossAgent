using System.Collections.Frozen;
using System.Text.Json;
using MossAgent.Domain;

namespace MossAgent.Application.Models;

public static class ModelCapabilityCatalog
{
    public static ModelCapabilityData Data { get; } = Load();
    private static readonly FrozenDictionary<string, ModelCapabilities> Qualified = Data.Models
        .ToFrozenDictionary(item => item.Provider + "/" + item.ModelId, StringComparer.OrdinalIgnoreCase);
    private static readonly FrozenDictionary<string, ModelCapabilities> Unqualified = Data.Models
        .GroupBy(item => item.ModelId, StringComparer.OrdinalIgnoreCase)
        .Where(group => group.Count() == 1)
        .ToFrozenDictionary(group => group.Key, group => group.Single(), StringComparer.OrdinalIgnoreCase);

    public static ModelCapabilities? Find(ProviderProtocol? protocol, string? modelId)
    {
        var id = modelId?.Trim() ?? string.Empty;
        if (protocol == ProviderProtocol.GeminiGenerateContent && id.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
            id = id[7..];
        if (!Qualified.TryGetValue(id, out var model) && !Unqualified.TryGetValue(id, out model)) return null;
        return protocol switch
        {
            ProviderProtocol.GeminiGenerateContent when model.Provider != "google" => null,
            ProviderProtocol.AnthropicMessages when model.Provider != "anthropic" => null,
            _ => model
        };
    }

    private static ModelCapabilityData Load()
    {
        using var stream = typeof(ModelCapabilityCatalog).Assembly.GetManifestResourceStream(
            "MossAgent.Application.Data.model-capabilities.json")
            ?? throw new InvalidOperationException("Missing bundled model capabilities.");
        var data = JsonSerializer.Deserialize<ModelCapabilityData>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Invalid bundled model capabilities.");
        if (data.SchemaVersion != 1 || data.Models.IsDefaultOrEmpty)
            throw new InvalidOperationException("Unsupported bundled model capabilities.");
        return data;
    }
}
