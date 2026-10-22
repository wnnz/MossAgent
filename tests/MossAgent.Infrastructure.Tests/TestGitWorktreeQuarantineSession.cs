using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Infrastructure.Tests;

internal sealed class TestGitWorktreeQuarantineSession(
    string source,
    string destination,
    IReadOnlyList<string> authorizedRoots) : IPathQuarantineSession
{
    public bool Moved { get; private set; }

    public async Task MoveAsync(Func<CancellationToken, Task> validateAuthorization,
        CancellationToken cancellationToken)
    {
        EnsureAuthorized(source);
        EnsureAuthorized(destination);
        await validateAuthorization(cancellationToken);
        Directory.Move(source, destination);
        Moved = true;
    }

    public async Task DeleteMovedAsync(Func<CancellationToken, Task> validateAuthorization,
        CancellationToken cancellationToken)
    {
        if (!Moved) throw new InvalidOperationException();
        EnsureAuthorized(destination);
        await validateAuthorization(cancellationToken);
        Directory.Delete(destination, recursive: true);
    }

    private void EnsureAuthorized(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!authorizedRoots.Select(Path.GetFullPath).Any(root =>
                fullPath.Equals(root, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(root + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException();
    }

    public void Dispose() { }
}
