using MossAgent.Platform.Windows.Resources;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

public sealed class WindowsAuthorizedDirectoryCreator : IAuthorizedDirectoryCreator
{
    public async Task<int> CreateAsync(string path, IReadOnlyList<string> authorizedRoots,
        Func<CancellationToken, Task> validateAuthorization, CancellationToken cancellationToken)
    {
        using var lease = await AcquireAsync(path, authorizedRoots, validateAuthorization, cancellationToken).ConfigureAwait(false);
        return lease.CreatedCount;
    }

    public Task<IAuthorizedDirectoryLease> AcquireAsync(string path, IReadOnlyList<string> authorizedRoots,
        Func<CancellationToken, Task> validateAuthorization, CancellationToken cancellationToken) =>
        Task.Run(() => CreateCoreAsync(path, authorizedRoots, validateAuthorization, cancellationToken), cancellationToken);

    private static async Task<IAuthorizedDirectoryLease> CreateCoreAsync(string path, IReadOnlyList<string> roots,
        Func<CancellationToken, Task> validate, CancellationToken token)
    {
        var created = 0;
        WindowsWriteDirectoryLease? lease = null;
        try
        {
            if (!Path.IsPathFullyQualified(path)) throw new UnauthorizedAccessException();
            path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            var root = roots.Where(Path.IsPathFullyQualified).Select(value => Path.TrimEndingDirectorySeparator(Path.GetFullPath(value)))
                .OrderByDescending(value => value.Length).FirstOrDefault(value => path.Equals(value, StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(Path.EndsInDirectorySeparator(value) ? value : value + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
            if (root is null) throw new UnauthorizedAccessException();
            lease = WindowsWriteDirectoryLease.Acquire(Path.Combine(root, ".mossagent-directory-anchor"), roots, token, false);
            var relative = Path.GetRelativePath(root, path);
            if (relative != ".")
                foreach (var part in relative.Split(Path.DirectorySeparatorChar))
                {
                    await validate(token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    lease.AddDirectory(part, true, token, () => created++);
                }
            WindowsWriteDirectoryCasePolicy.Validate(lease.Parent);
            token.ThrowIfCancellationRequested();
            return new WindowsAuthorizedDirectoryLease(lease, created);
        }
        catch (Exception exception)
        {
            lease?.Dispose();
            if (created > 0)
                throw new FileMutationSessionException(WindowsFileText.Format("DirectoryCreateIncomplete", created),
                    "directory_create_incomplete", true);
            if (exception is IOException or UnauthorizedAccessException)
                throw new FileMutationSessionException(WindowsFileText.Get("DirectoryCreateDenied"), "directory_create_unsafe");
            throw;
        }
    }
}
