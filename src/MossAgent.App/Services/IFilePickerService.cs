namespace MossAgent.App.Services;

public interface IFilePickerService
{
    Task<IReadOnlyList<string>> PickFilesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> PickPreviewFilesAsync(CancellationToken cancellationToken) => PickFilesAsync(cancellationToken);
    Task<IReadOnlyList<string>> PickPluginPackageAsync(CancellationToken cancellationToken) => PickFilesAsync(cancellationToken);
}
