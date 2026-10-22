using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Infrastructure.Tests;

internal sealed class TestGitWorktreeDeleteSession(
    string path,
    IReadOnlyList<string> authorizedRoots,
    bool recursive,
    bool fileOnly) : IPathDeleteSession
{
    public Stream? FileContent => null;
    public int PlannedCount => 1;
    public int DeletedCount { get; private set; }

    public async Task DeleteAsync(
        Func<CancellationToken, Task> validateAuthorization,
        CancellationToken cancellationToken)
    {
        EnsureAuthorized();
        await validateAuthorization(cancellationToken);
        if (Directory.Exists(path))
        {
            if (fileOnly || !recursive)
                throw new InvalidOperationException("Test deletion request does not match target.");
            Directory.Delete(path, recursive: true);
            DeletedCount++;
            return;
        }
        if (File.Exists(path) && fileOnly)
        {
            File.Delete(path);
            DeletedCount++;
        }
        else throw new FileNotFoundException("Test deletion target was not found.", path);
    }

    private void EnsureAuthorized()
    {
        var fullPath = Path.GetFullPath(path);
        if (!authorizedRoots.Select(Path.GetFullPath).Any(root =>
                fullPath.StartsWith(root + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException();
    }

    public void Dispose() { }
}
