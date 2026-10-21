namespace MossAgent.Tools.Abstractions.Files;

public interface IFileTreeReadSession : IDisposable
{
    IReadOnlyList<FileTreeReadEntry> Entries { get; }
}
