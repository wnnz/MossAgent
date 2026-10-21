using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using MossAgent.Platform.Windows.Resources;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

internal sealed class WindowsPathMoveSession : IPathMoveSession
{
    private readonly WindowsWriteDirectoryLease _sourceParent;
    private readonly SafeFileHandle _source;
    private readonly string _destination;
    private readonly IReadOnlyList<string> _roots;
    private bool _disposed;
    public FileStream? FileContent { get; }
    public bool Moved { get; private set; }

    public WindowsPathMoveSession(string source, string destination, IReadOnlyList<string> roots,
        WindowsWriteDirectoryLease sourceParent, CancellationToken token)
    {
        _sourceParent = sourceParent;
        _destination = destination;
        _roots = roots;
        token.ThrowIfCancellationRequested();
        _source = WindowsRelativeFile.Open(sourceParent.Parent, Path.GetFileName(source),
            WindowsRelativeFile.ReadAccess | 0x10000, FileShare.Read, 1, null, true)
            ?? throw new FileMutationSessionException(WindowsFileText.Get("MoveNotFound"), "path_not_found");
        try
        {
            var attributes = File.GetAttributes(_source);
            if ((attributes & FileAttributes.ReparsePoint) != 0) throw new UnauthorizedAccessException(WindowsFileText.Get("MoveDenied"));
            if ((attributes & FileAttributes.Directory) == 0) FileContent = new FileStream(_source, FileAccess.Read, 81920, false);
            else WindowsWriteDirectoryCasePolicy.Validate(_source);
        }
        catch { _source.Dispose(); throw; }
    }

    public async Task<bool> TryCommitAsync(string? expectedDestinationSha256,
        Func<CancellationToken, Task> validateAuthorization, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Moved) throw new InvalidOperationException();
        await validateAuthorization(cancellationToken).ConfigureAwait(false);
        using var parent = WindowsWriteDirectoryLease.Acquire(_destination, _roots, cancellationToken, false);
        using var target = WindowsRelativeFile.Open(parent.Parent, Path.GetFileName(_destination),
            WindowsRelativeFile.ReadAccess | 0x10000, FileShare.Read | FileShare.Delete, 1, null, true);
        if (target is null && expectedDestinationSha256 is not null
            || target is not null && (expectedDestinationSha256 is null || FileContent is null)) return false;
        if (target is not null)
        {
            if ((File.GetAttributes(target) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) return false;
            WindowsFileReplacementMetadata.Validate(target);
            using var stream = new FileStream(target, FileAccess.Read, 81920, false);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
            if (!hash.Equals(expectedDestinationSha256, StringComparison.OrdinalIgnoreCase)) return false;
            await validateAuthorization(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            WindowsRelativeFile.Rename(_source, parent.Parent, Path.GetFileName(_destination), true);
        }
        else
        {
            await validateAuthorization(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            WindowsRelativeFile.Rename(_source, parent.Parent, Path.GetFileName(_destination), false);
        }
        Moved = true;
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { FileContent?.Dispose(); }
        finally { try { _source.Dispose(); } finally { _sourceParent.Dispose(); } }
    }
}
