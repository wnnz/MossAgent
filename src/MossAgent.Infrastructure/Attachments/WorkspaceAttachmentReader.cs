using MossAgent.Application.Attachments;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Attachments;

public sealed class WorkspaceAttachmentReader(IAttachmentImageDecoder? images = null, TimeProvider? timeProvider = null)
    : IWorkspaceAttachmentReader, ISelectedAttachmentReader
{
    private readonly AttachmentContentCodec _codec = new(images);

    public Task<IReadOnlyList<WorkspaceAttachmentContent>> ReadAsync(
        ProjectProfile project,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
        => ReadBoundedAsync(project with { AuthorizedDirectories = project.AuthorizedDirectories.ToArray() }, paths, cancellationToken);

    public Task<IReadOnlyList<WorkspaceAttachmentContent>> ReadSelectedAsync(
        IReadOnlyList<string> selectedAbsolutePaths, CancellationToken cancellationToken) =>
        ReadBoundedAsync(null, selectedAbsolutePaths, cancellationToken);

    private async Task<IReadOnlyList<WorkspaceAttachmentContent>> ReadBoundedAsync(ProjectProfile? project,
        IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        var selected = paths.ToArray();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30), timeProvider ?? TimeProvider.System);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var token = cancellation.Token;
        return await Task.Run(() => ReadCoreAsync(project, selected, token), token).WaitAsync(token);
    }

    private async Task<IReadOnlyList<WorkspaceAttachmentContent>> ReadCoreAsync(ProjectProfile? project,
        IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        if (paths.Count > WorkspaceAttachmentLimits.MaximumFiles)
        {
            throw new InvalidOperationException(
                $"每次最多附加 {WorkspaceAttachmentLimits.MaximumFiles} 个文件。");
        }

        var roots = project is null ? null : project.AuthorizedDirectories
            .Append(project.PrimaryDirectory)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var results = new List<WorkspaceAttachmentContent>(paths.Count);
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await ReadOneAsync(path, roots, cancellationToken));
            if (results.Where(item => item.Image is not null).Sum(item => item.ByteCount) > AttachmentImage.MaximumTotalByteCount)
                throw new AttachmentImageValidationException(AttachmentImageFailure.SizeLimit);
            var documents = results.Where(item => item.Document is not null).ToArray();
            if (documents.Length > AttachmentDocument.MaximumCount
                || documents.Sum(item => item.ByteCount) > AttachmentDocument.MaximumTotalByteCount)
                throw new InvalidDataException("PDF attachments exceed their aggregate limit.");
        }

        return results;
    }

    private async Task<WorkspaceAttachmentContent> ReadOneAsync(
        string path,
        IReadOnlyList<string>? roots,
        CancellationToken cancellationToken)
    {
        if (roots is null && (!Path.IsPathFullyQualified(path) || OperatingSystem.IsWindows()
            && (path.StartsWith(@"\\.\", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(@"\\?\GLOBALROOT", StringComparison.OrdinalIgnoreCase)
                || path[(Path.GetPathRoot(path)?.Length ?? 0)..].Contains(':'))))
            throw new UnauthorizedAccessException("Selected attachment paths must be absolute.");
        var fullPath = Path.GetFullPath(path);
        var root = roots?
            .Where(candidate => IsWithin(fullPath, candidate))
            .OrderByDescending(static candidate => candidate.Length)
            .FirstOrDefault();
        if (roots is not null && root is null) throw new UnauthorizedAccessException("附件必须位于项目授权目录中。");
        RejectLinkEscape(fullPath, roots);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("所选附件不存在。", fullPath);
        }

        await using var stream = new FileStream(
            fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            16 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var bytes = await ReadBytesAsync(stream, fullPath, cancellationToken);
        RejectLinkEscape(fullPath, roots);
        var displayPath = root is null ? Path.GetFileName(fullPath) : BuildDisplayPath(root, fullPath);
        try { return await _codec.DecodeAsync(fullPath, displayPath, bytes, null, cancellationToken); }
        catch (InvalidDataException exception) when (
            AttachmentContentCodec.IsPdfPath(fullPath)
            || AttachmentDocument.DetectMediaType(bytes) is not null)
        {
            throw new InvalidDataException("PDF 附件结构无效或已损坏。", exception);
        }
        catch (InvalidDataException exception) { throw new InvalidOperationException("不支持二进制文本附件。", exception); }
        catch (System.Text.DecoderFallbackException exception) { throw new InvalidOperationException("附件不是有效的 UTF-8 文本文件。", exception); }
    }

    private static async Task<byte[]> ReadBytesAsync(FileStream stream, string path, CancellationToken cancellationToken)
    {
        var prefix = new byte[(int)Math.Min(12, stream.Length)];
        await stream.ReadExactlyAsync(prefix, cancellationToken);
        var image = AttachmentImage.DetectMediaType(prefix) is not null || AttachmentContentCodec.IsImagePath(path);
        var document = AttachmentDocument.DetectMediaType(prefix) is not null
            || AttachmentContentCodec.IsPdfPath(path);
        var limit = image ? AttachmentImage.MaximumByteCount
            : document ? AttachmentDocument.MaximumByteCount
            : WorkspaceAttachmentLimits.MaximumBytesPerFile;
        if (stream.Length > limit)
        {
            if (image) throw new AttachmentImageValidationException(AttachmentImageFailure.SizeLimit);
            if (document)
                throw new InvalidDataException(
                    $"PDF attachments cannot exceed {AttachmentDocument.MaximumByteCount / 1024 / 1024} MiB.");
            throw new InvalidOperationException(
                $"附件不能超过 {WorkspaceAttachmentLimits.MaximumBytesPerFile / 1024} KiB。");
        }
        var bytes = new byte[checked((int)stream.Length)];
        prefix.CopyTo(bytes, 0);
        await stream.ReadExactlyAsync(bytes.AsMemory(prefix.Length), cancellationToken);
        if (stream.Length != bytes.Length) throw new IOException("Attachment changed while reading.");
        return bytes;
    }

    private static string BuildDisplayPath(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        var rootName = Path.GetFileName(Path.TrimEndingDirectorySeparator(root));
        return string.IsNullOrWhiteSpace(rootName)
            ? relative
            : Path.Combine(rootName, relative);
    }

    private static bool IsWithin(string candidate, string root)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        return candidate.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(
                normalizedRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
    }

    private static void RejectLinkEscape(string candidate, IReadOnlyList<string>? roots)
    {
        var current = candidate;
        while (!string.IsNullOrWhiteSpace(current))
        {
            FileSystemInfo? info = File.Exists(current)
                ? new FileInfo(current)
                : Directory.Exists(current) ? new DirectoryInfo(current) : null;
            if (roots is null && info is not null && (info.LinkTarget is not null || (info.Attributes & FileAttributes.ReparsePoint) != 0))
                throw new UnauthorizedAccessException("Selected attachments cannot traverse links or reparse points.");
            if (info?.LinkTarget is not null)
            {
                var target = info.ResolveLinkTarget(true)?.FullName;
                if (target is not null && (roots is null || !roots.Any(root => IsWithin(target, root))))
                {
                    throw new UnauthorizedAccessException("附件路径中的链接指向授权目录之外。");
                }
            }

            var parent = Path.GetDirectoryName(current);
            if (parent is null || parent.Equals(current, StringComparison.OrdinalIgnoreCase)) break;
            current = parent;
        }
    }
}
