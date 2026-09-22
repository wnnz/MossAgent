using CodingAgent.Domain.Interfaces;

namespace CodingAgent.Infrastructure.Storage;

/// <summary>沙箱：文件工具仅允许访问工作区目录内路径，拒绝路径穿越与绝对路径逃逸。</summary>
public class WorkspaceGuard : IWorkspaceGuard
{
    public WorkspaceGuard(string workspaceRoot)
    {
        WorkspaceRoot = Path.GetFullPath(workspaceRoot);
        Directory.CreateDirectory(WorkspaceRoot);
    }

    public string WorkspaceRoot { get; }

    public string ResolveInsideWorkspace(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return WorkspaceRoot;
        }

        string full;
        if (Path.IsPathRooted(path))
        {
            full = Path.GetFullPath(path);
        }
        else
        {
            full = Path.GetFullPath(Path.Combine(WorkspaceRoot, path));
        }

        EnsureInsideWorkspace(full);
        return full;
    }

    public void EnsureInsideWorkspace(string fullPath)
    {
        var root = WorkspaceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(fullPath);
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"路径超出工作区范围: {fullPath}");
        }
    }
}
