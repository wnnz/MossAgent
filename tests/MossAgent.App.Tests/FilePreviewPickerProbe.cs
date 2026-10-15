using MossAgent.App.Services;

namespace MossAgent.App.Tests;

internal sealed class FilePreviewPickerProbe : IFilePickerService
{
    public List<CancellationToken> Tokens { get; } = [];
    public Func<CancellationToken, Task<IReadOnlyList<string>>> Pick { get; set; } = _ => Task.FromResult<IReadOnlyList<string>>([]);
    public Task<IReadOnlyList<string>> PickFilesAsync(CancellationToken token) => throw new InvalidOperationException("Attachment picker must not be used.");
    public Task<IReadOnlyList<string>> PickPreviewFilesAsync(CancellationToken token)
    {
        Tokens.Add(token);
        return Pick(token);
    }
}
