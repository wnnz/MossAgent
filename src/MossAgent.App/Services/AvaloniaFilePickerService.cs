using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace MossAgent.App.Services;

public sealed class AvaloniaFilePickerService : IFilePickerService
{
    public async Task<IReadOnlyList<string>> PickFilesAsync(
        CancellationToken cancellationToken)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime
            is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            return [];
        }

        var files = await window.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "选择项目内的文本文件",
                AllowMultiple = true
            });
        cancellationToken.ThrowIfCancellationRequested();
        return files.Select(static file => file.Path.LocalPath).ToArray();
    }
}
