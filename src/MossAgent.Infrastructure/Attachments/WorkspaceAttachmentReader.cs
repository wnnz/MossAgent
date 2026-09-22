using System.Text;
using MossAgent.Application.Attachments;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Attachments;

public sealed class WorkspaceAttachmentReader : IWorkspaceAttachmentReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public async Task<IReadOnlyList<WorkspaceAttachmentContent>> ReadAsync(
        ProjectProfile project,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
    {
        if (paths.Count > WorkspaceAttachmentLimits.MaximumFiles)
        {
            throw new InvalidOperationException(
                $"每次最多附加 {WorkspaceAttachmentLimits.MaximumFiles} 个文件。");
        }

        var roots = project.AuthorizedDirectories
            .Append(project.PrimaryDirectory)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var results = new List<WorkspaceAttachmentContent>(paths.Count);
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await ReadOneAsync(path, roots, cancellationToken));
        }

        return results;
    }

    private static async Task<WorkspaceAttachmentContent> ReadOneAsync(
        string path,
        IReadOnlyList<string> roots,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(path);
        var root = roots
            .Where(candidate => IsWithin(fullPath, candidate))
            .OrderByDescending(static candidate => candidate.Length)
            .FirstOrDefault()
            ?? throw new UnauthorizedAccessException("附件必须位于项目授权目录中。");
        RejectLinkEscape(fullPath, roots);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("所选附件不存在。", fullPath);
        }

        await using var stream = new FileStream(
            fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            16 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length > WorkspaceAttachmentLimits.MaximumBytesPerFile)
        {
            throw new InvalidOperationException(
                $"附件不能超过 {WorkspaceAttachmentLimits.MaximumBytesPerFile / 1024} KiB。");
        }

        var bytes = new byte[stream.Length];
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        if (bytes.Contains((byte)0))
        {
            throw new InvalidOperationException("不支持二进制附件。");
        }

        string content;
        try
        {
            content = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidOperationException("附件不是有效的 UTF-8 文本文件。", exception);
        }

        return new WorkspaceAttachmentContent(
            fullPath, BuildDisplayPath(root, fullPath), content, bytes.LongLength);
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

    private static void RejectLinkEscape(string candidate, IReadOnlyList<string> roots)
    {
        var current = candidate;
        while (!string.IsNullOrWhiteSpace(current))
        {
            FileSystemInfo? info = File.Exists(current)
                ? new FileInfo(current)
                : Directory.Exists(current) ? new DirectoryInfo(current) : null;
            if (info?.LinkTarget is not null)
            {
                var target = info.ResolveLinkTarget(true)?.FullName;
                if (target is not null && !roots.Any(root => IsWithin(target, root)))
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
