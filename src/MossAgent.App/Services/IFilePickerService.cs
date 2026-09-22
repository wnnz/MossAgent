namespace MossAgent.App.Services;

public interface IFilePickerService
{
    Task<IReadOnlyList<string>> PickFilesAsync(CancellationToken cancellationToken);
}
