using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Processes;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class ProcessIsolationSpecTests
{
    [Fact]
    public void EmptyKey_IsRejected()
    {
        var root = Path.GetTempPath();

        Assert.Throws<ArgumentException>(() => new ProcessIsolationSpec("", [root], []));
    }

    [Theory]
    [InlineData("bad key")]
    [InlineData("bad/key")]
    [InlineData("bad\\key")]
    [InlineData("bad;key")]
    public void IllegalKeyCharacters_AreRejected(string key)
    {
        var root = Path.GetTempPath();

        Assert.Throws<ArgumentException>(() => new ProcessIsolationSpec(key, [root], []));
    }

    [Fact]
    public void EmptyWritableRoots_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => new ProcessIsolationSpec("valid-key_1.0", [], []));
    }

    [Theory]
    [InlineData("relative")]
    [InlineData("..\\parent")]
    public void RelativePaths_AreRejected(string path)
    {
        var root = Path.GetTempPath();

        Assert.Throws<ArgumentException>(() => new ProcessIsolationSpec("valid-key", [root, path], []));
        Assert.Throws<ArgumentException>(() => new ProcessIsolationSpec("valid-key", [root], [path]));
        Assert.Throws<ArgumentException>(() => new ProcessIsolationSpec("valid-key", [root], [], path));
    }

    [Fact]
    public void ContextIsolation_DerivesStableKeyNormalizedRootsAndPrivateDirectory()
    {
        var root = Path.GetTempPath();
        var artifact = Path.Combine(Path.GetTempPath(), "artifacts-" + Guid.NewGuid().ToString("N"));
        var context = new ToolExecutionContext(Guid.NewGuid(), root, [root + "." + Path.DirectorySeparatorChar],
            ApprovalPolicy.FullAccess, ArtifactDirectory: artifact);

        var spec = context.CreateProcessIsolation();
        var repeat = context.CreateProcessIsolation();

        Assert.Equal(spec.SandboxKey, repeat.SandboxKey);
        Assert.StartsWith($"mossagent-{context.TaskId:N}-", spec.SandboxKey);
        Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)), Assert.Single(spec.WritableRoots));
        Assert.Equal(Path.Combine(Path.GetFullPath(artifact), "sandbox-tmp"), spec.PrivateDirectory);
        Assert.True(spec.AllowOutboundNetwork);
        Assert.Empty(spec.ReadOnlyPaths);
    }

    [Fact]
    public void ContextIsolation_KeyChangesWithRoots()
    {
        var root = Path.GetTempPath();
        var task = Guid.NewGuid();
        var first = new ToolExecutionContext(task, root, [root], ApprovalPolicy.FullAccess).CreateProcessIsolation();
        var second = new ToolExecutionContext(task, root, [root, Environment.GetFolderPath(Environment.SpecialFolder.Windows)],
            ApprovalPolicy.FullAccess).CreateProcessIsolation();

        Assert.NotEqual(first.SandboxKey, second.SandboxKey);
    }
}
