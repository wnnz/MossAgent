using System.Text.Json;
using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;

namespace MossAgent.Infrastructure.Plugins;

/// <summary>Bounded parse of the required root plugin.json. Only identity fields and the
/// overlay decision are extracted; an object-valued extensions.com.openai wholly replaces
/// the legacy .codex-plugin/plugin.json, which is then never opened.</summary>
internal static class PluginManifestParser
{
    private const string ManifestName = "plugin.json";
    private const string CompatibilityName = ".codex-plugin/plugin.json";

    public static async Task<PluginManifestData> ReadAsync(IReadOnlyList<PluginArchiveEntry> entries, CancellationToken token)
    {
        var manifest = PluginEntryLookup.FindFile(entries, ManifestName);
        if (manifest is null)
            throw new PluginPackageException(PluginPackageErrorCode.ManifestMissing, "root plugin.json is required");
        var bytes = await PluginEntryReader.ReadAllAsync(manifest, PluginArchiveLimits.ManifestBytes,
            PluginPackageErrorCode.ManifestInvalid, token);
        using var document = Parse(bytes);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw Invalid("root plugin.json must contain a JSON object");
        var overlay = HasOpenAiExtension(root)
            ? PluginOverlaySource.ExtensionsObject
            : await ReadCompatibilityOverlayAsync(entries, token);
        return new PluginManifestData(RequiredText(root, "name", 200), RequiredText(root, "version", 100),
            OptionalText(root, "description"), overlay);
    }

    private static bool HasOpenAiExtension(JsonElement root) =>
        root.TryGetProperty("extensions", out var extensions) && extensions.ValueKind == JsonValueKind.Object
        && extensions.TryGetProperty("com.openai", out var overlay) && overlay.ValueKind == JsonValueKind.Object;

    private static async Task<PluginOverlaySource> ReadCompatibilityOverlayAsync(
        IReadOnlyList<PluginArchiveEntry> entries, CancellationToken token)
    {
        var compatibility = PluginEntryLookup.FindFile(entries, CompatibilityName);
        if (compatibility is null) return PluginOverlaySource.None;
        var bytes = await PluginEntryReader.ReadAllAsync(compatibility, PluginArchiveLimits.CompatibilityManifestBytes,
            PluginPackageErrorCode.ManifestInvalid, token);
        using var document = Parse(bytes);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw Invalid(".codex-plugin/plugin.json must contain a JSON object");
        return PluginOverlaySource.CompatibilityFile;
    }

    private static string RequiredText(JsonElement root, string property, int maximumLength)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
            throw Invalid($"root plugin.json requires a non-empty '{property}' string");
        var text = value.GetString()!;
        if (string.IsNullOrWhiteSpace(text) || text.Length > maximumLength)
            throw Invalid($"root plugin.json requires a non-empty '{property}' string");
        return text;
    }

    private static string? OptionalText(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String) throw Invalid($"'{property}' must be a string when present");
        return value.GetString();
    }

    private static JsonDocument Parse(byte[] bytes)
    {
        try
        {
            return JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = PluginArchiveLimits.JsonMaximumDepth });
        }
        catch (JsonException error)
        {
            throw new PluginPackageException(PluginPackageErrorCode.ManifestInvalid, "manifest JSON is malformed", error);
        }
    }

    private static PluginPackageException Invalid(string detail) => new(PluginPackageErrorCode.ManifestInvalid, detail);
}
