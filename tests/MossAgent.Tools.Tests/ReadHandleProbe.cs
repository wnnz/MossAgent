using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Tools.Tests;

internal sealed class ReadHandleProbe : IFileReadHandleValidator
{
    private int _calls;
    public int Calls => Volatile.Read(ref _calls);
    public FileStream? Stream { get; private set; }
    public Action<FileStream, IReadOnlyList<string>>? Check { get; init; }

    public void Validate(FileStream stream, IReadOnlyList<string> authorizedRoots)
    {
        Interlocked.Increment(ref _calls);
        Stream = stream;
        Check?.Invoke(stream, authorizedRoots);
    }
}
