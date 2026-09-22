using CodingAgent.Infrastructure.Storage;
using Xunit;

namespace CodingAgent.Tests;

public class WorkspaceGuardTests : IDisposable
{
    private readonly string _root;
    private readonly WorkspaceGuard _guard;

    public WorkspaceGuardTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "codingagent-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _guard = new WorkspaceGuard(_root);
    }

    [Fact]
    public void ResolveInsideWorkspace_RelativePath_ReturnsAbsoluteInside()
    {
        var resolved = _guard.ResolveInsideWorkspace("sub/file.txt");
        Assert.StartsWith(Path.GetFullPath(_root), resolved, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveInsideWorkspace_DotDotTraversal_Throws()
    {
        Assert.Throws<UnauthorizedAccessException>(() => _guard.ResolveInsideWorkspace("../outside.txt"));
    }

    [Fact]
    public void ResolveInsideWorkspace_DeepTraversal_Throws()
    {
        Assert.Throws<UnauthorizedAccessException>(() => _guard.ResolveInsideWorkspace("a/b/../../../etc/passwd"));
    }

    [Fact]
    public void ResolveInsideWorkspace_AbsoluteOutside_Throws()
    {
        var outside = Path.Combine(Path.GetTempPath(), "outside-secret.txt");
        Assert.Throws<UnauthorizedAccessException>(() => _guard.ResolveInsideWorkspace(outside));
    }

    [Fact]
    public void ResolveInsideWorkspace_AbsoluteInside_Allowed()
    {
        var inside = Path.Combine(_root, "in-file.txt");
        var resolved = _guard.ResolveInsideWorkspace(inside);
        Assert.Equal(Path.GetFullPath(inside), resolved);
    }

    [Fact]
    public void ResolveInsideWorkspace_SimilarPrefixDirectory_Throws()
    {
        // 构造 root 同前缀但不同的目录：root2 ≠ root
        var root2 = _root.TrimEnd(Path.DirectorySeparatorChar) + "2";
        Directory.CreateDirectory(root2);
        try
        {
            Assert.Throws<UnauthorizedAccessException>(() => _guard.ResolveInsideWorkspace(Path.Combine(root2, "x.txt")));
        }
        finally
        {
            Directory.Delete(root2, true);
        }
    }

    [Fact]
    public void ResolveInsideWorkspace_Empty_ReturnsRoot()
    {
        Assert.Equal(Path.GetFullPath(_root), _guard.ResolveInsideWorkspace(""));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch (IOException) { }
    }
}
