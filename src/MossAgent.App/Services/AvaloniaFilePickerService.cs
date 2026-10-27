using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using MossAgent.App.Resources;

namespace MossAgent.App.Services;

public sealed class AvaloniaFilePickerService : IFilePickerService
{
    public Task<IReadOnlyList<string>> PickFilesAsync(CancellationToken cancellationToken) =>
        PickAsync(InspectorText.Get("PreviewAttachmentSelection"), CreateFilters(), allowMultiple: true, cancellationToken);

    public Task<IReadOnlyList<string>> PickPreviewFilesAsync(CancellationToken cancellationToken) =>
        PickAsync(InspectorText.Get("PreviewFileSelection"), CreateFilters(), allowMultiple: true, cancellationToken);

    public Task<IReadOnlyList<string>> PickPluginPackageAsync(CancellationToken cancellationToken) =>
        PickAsync(PluginPackageLabels.Pick, CreatePackageFilters(), allowMultiple: false, cancellationToken);

    private static async Task<IReadOnlyList<string>> PickAsync(
        string title, IReadOnlyList<FilePickerFileType> filters, bool allowMultiple, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Avalonia.Application.Current?.ApplicationLifetime
            is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            return [];
        }

        var files = await window.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = allowMultiple,
                FileTypeFilter = filters
            });
        cancellationToken.ThrowIfCancellationRequested();
        return files.Select(static file => file.Path.LocalPath).ToArray();
    }

    internal static IReadOnlyList<FilePickerFileType> CreateFilters() =>
    [
        FilePickerFileTypes.All,
        new FilePickerFileType(InspectorText.Get("PreviewFilterSupported"))
        {
            Patterns = ["*.txt", "*.md", "*.json", "*.csv", "*.log",
                "*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp", "*.pdf"]
        },
        new FilePickerFileType(InspectorText.Get("PreviewFilterDocuments")) { Patterns = ["*.pdf"] }
    ];

    internal static IReadOnlyList<FilePickerFileType> CreatePackageFilters() =>
    [
        new FilePickerFileType(SettingsText.Get("PluginPackageFilter")) { Patterns = ["*.zip", "*.mossplugin"] },
        FilePickerFileTypes.All
    ];
}
