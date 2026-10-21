using System.Text;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class AtomicWriteDirectorySafetyTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ParentCannotMoveAfterStaging_AndIsReleasedAfterCommit(bool existed)
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var parent = Directory.CreateDirectory(Path.Combine(fixture.Root, "parent")).FullName;
        var target = Path.Combine(parent, "edit.txt");
        if (existed) await File.WriteAllTextAsync(target, "original", TestContext.Current.CancellationToken);
        var checkedStaged = false;
        var roots = new WritePhaseRoots(fixture.Root, () =>
        {
            if (Directory.GetFiles(parent, ".mossagent-*.tmp").Length == 0) return;
            checkedStaged = true;
            Assert.Throws<IOException>(() => Directory.Move(parent, parent + "-moved"));
            Assert.Throws<IOException>(() => Directory.Move(fixture.Root, fixture.Root + "-moved"));
        });
        var context = new ToolExecutionContext(Guid.NewGuid(), fixture.Root, roots, ApprovalPolicy.FullAccess);
        var writer = new AtomicTextFileWriter(new AuthorizedPathResolver(), new WindowsAtomicFileWriteSessionFactory());
        var original = await FileSnapshotReader.ReadAsync(target, TestContext.Current.CancellationToken);

        await writer.WriteAsync(target, original, context, async (stream, token) =>
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes("updated"), token);
        }, TestContext.Current.CancellationToken);

        Assert.True(checkedStaged);
        Assert.Equal("updated", await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken));
        Directory.Move(parent, parent + "-moved");
        Assert.Empty(Directory.GetFiles(fixture.Root, ".mossagent-*.tmp", SearchOption.AllDirectories));
    }
}
