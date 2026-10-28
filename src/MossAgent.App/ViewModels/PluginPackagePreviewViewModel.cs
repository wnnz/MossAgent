using System.Globalization;
using MossAgent.App.Resources;
using MossAgent.Domain.Plugins;

namespace MossAgent.App.ViewModels;

/// <summary>Presentation of a local package preview awaiting install confirmation.
/// <see cref="PackagePath"/> is the caller-picked file, not the preview's echoed source, so
/// confirmation always installs the path the user selected. <see cref="ReplaceNotice"/> warns
/// when confirmation will upgrade-replace already-installed rows of the same name.</summary>
public sealed class PluginPackagePreviewViewModel
{
    public PluginPackagePreviewViewModel(PluginPackagePreview preview, string packagePath,
        IReadOnlyList<InstalledPlugin>? replaced = null)
    {
        PackagePath = packagePath;
        Name = preview.Name;
        Version = preview.Version;
        Title = $"{preview.Name} {preview.Version}";
        Description = preview.Description ?? string.Empty;
        ComponentLines = preview.Components
            .Select(static component => $"{ComponentKindText(component.Kind)} {component.Name}")
            .ToArray();
        ComponentsHeader = preview.Components.Count == 0
            ? SettingsText.Get("PluginPreviewNoComponents")
            : string.Format(CultureInfo.CurrentCulture, SettingsText.Get("PluginPreviewComponents"), preview.Components.Count);
        McpServersText = preview.DeclaredMcpServers.Count == 0
            ? SettingsText.Get("PluginPreviewNoMcpServers")
            : string.Format(CultureInfo.CurrentCulture, SettingsText.Get("PluginPreviewMcpServers"),
                string.Join("、", preview.DeclaredMcpServers.Select(static server => $"{server.Name}（{server.TransportType}）")));
        HashText = string.Format(CultureInfo.CurrentCulture, SettingsText.Get("PluginPreviewHash"),
            preview.ContentHash.Length <= 12 ? preview.ContentHash : preview.ContentHash[..12]);
        SourceText = string.Format(CultureInfo.CurrentCulture, SettingsText.Get("PluginPreviewSource"), packagePath);
        ReplaceNotice = replaced is { Count: > 0 } rows
            ? string.Format(CultureInfo.CurrentCulture, SettingsText.Get("PluginUpgradeNotice"),
                preview.Name, string.Join("、", rows.Select(static row => $"v{row.Version}")))
            : string.Empty;
    }

    public string PackagePath { get; }
    public string Name { get; }
    public string Version { get; }
    public string Title { get; }
    public string Description { get; }
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public IReadOnlyList<string> ComponentLines { get; }
    public string ComponentsHeader { get; }
    public string McpServersText { get; }
    public string HashText { get; }
    public string SourceText { get; }
    public string ReplaceNotice { get; }
    public bool HasReplaceNotice => ReplaceNotice.Length > 0;

    private static string ComponentKindText(PluginComponentKind kind) => kind switch
    {
        PluginComponentKind.Skill => SettingsText.Get("PluginComponentSkill"),
        PluginComponentKind.McpServer => SettingsText.Get("PluginComponentMcpServer"),
        _ => SettingsText.Get("PluginComponentAsset")
    };
}
