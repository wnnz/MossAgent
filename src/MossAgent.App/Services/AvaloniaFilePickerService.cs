using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using MossAgent.App.Resources;

namespace MossAgent.App.Services;

public sealed class AvaloniaFilePickerService : IFilePickerService
{
    public Task<IReadOnlyList<string>> PickFilesAsync(CancellationToken cancellationToken) =>
        PickAsync(InspectorText.Get("PreviewAttachmentSelection"), cancellationToken);

    public Task<IReadOnlyList<string>> PickPreviewFilesAsync(CancellationToken cancellationToken) =>
        PickAsync(InspectorText.Get("PreviewFileSelection"), cancellationToken);

    private static async Task<IReadOnlyList<string>> PickAsync(string title, CancellationToken cancellationToken)
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
                AllowMultiple = true
            });
        cancellationToken.ThrowIfCancellationRequested();
        return files.Select(static file => file.Path.LocalPath).ToArray();
    }
}
