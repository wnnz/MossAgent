using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace MossAgent.App.Services;

public sealed class AvaloniaFolderPickerService : IFolderPickerService
{
    public async Task<string?> PickFolderAsync(CancellationToken cancellationToken)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime
            is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            return null;
        }

        var folders = await window.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "选择项目目录",
                AllowMultiple = false
            });
        cancellationToken.ThrowIfCancellationRequested();
        return folders.FirstOrDefault()?.Path.LocalPath;
    }
}
