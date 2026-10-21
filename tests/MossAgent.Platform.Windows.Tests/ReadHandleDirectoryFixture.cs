namespace MossAgent.Platform.Windows.Tests;

internal sealed class ReadHandleDirectoryFixture : IDisposable
{
    public string Root { get; } = Directory.CreateDirectory(Path.Combine(
        Path.GetTempPath(), "MossAgent.Handle.Tests", Guid.NewGuid().ToString("N"))).FullName;

    public string DirectoryFor(string name) => Directory.CreateDirectory(Path.Combine(Root, name)).FullName;

    public string FileFor(string directory, string name = "file.txt")
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, "handle fixture");
        return path;
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);
}
