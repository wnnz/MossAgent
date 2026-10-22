using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class PathQuarantineSessionTests
{
    [Fact]
    public async Task MoveAndDeleteUsesPinnedHandlesAndLeavesLaterSourceReplacementUntouched()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests",
            Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var destination = Path.Combine(root, "quarantine");
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        await File.WriteAllTextAsync(Path.Combine(source, "nested", "owned.txt"),
            "owned", TestContext.Current.CancellationToken);
        try
        {
            using var session = new WindowsPathQuarantineSessionFactory().Create(
                source, destination, [root], TestContext.Current.CancellationToken);
            Assert.Throws<IOException>(() => Directory.Move(source, source + "-renamed"));
            await session.MoveAsync(static _ => Task.CompletedTask,
                TestContext.Current.CancellationToken);
            Assert.False(Directory.Exists(source));
            Assert.True(Directory.Exists(destination));

            Directory.CreateDirectory(source);
            await File.WriteAllTextAsync(Path.Combine(source, "replacement.txt"),
                "replacement", TestContext.Current.CancellationToken);
            await session.DeleteMovedAsync(static _ => Task.CompletedTask,
                TestContext.Current.CancellationToken);
            session.Dispose();

            Assert.False(Directory.Exists(destination));
            Assert.Equal("replacement", await File.ReadAllTextAsync(
                Path.Combine(source, "replacement.txt"),
                TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(root))
                await WindowsProcessTestDirectory.DeleteAsync(root);
        }
    }

    [Fact]
    public async Task RootIdentityHandleCanBeTransferredIntoQuarantineWithoutReopeningThePath()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests",
            Guid.NewGuid().ToString("N"));
        var quarantineRoot = root + "-quarantine";
        var destination = Path.Combine(quarantineRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(quarantineRoot);
        await File.WriteAllTextAsync(Path.Combine(root, "owned.txt"), "owned",
            TestContext.Current.CancellationToken);
        try
        {
            var factory = new WindowsRootIdentitySessionFactory();
            using var identity = await factory.OpenTransferableAsync(
                [root], TestContext.Current.CancellationToken);
            var source = Assert.IsAssignableFrom<IPathQuarantineSessionFactory>(identity);
            using var quarantine = source.Create(root, destination,
                [Path.GetDirectoryName(destination)!], TestContext.Current.CancellationToken);
            await quarantine.MoveAsync(static _ => Task.CompletedTask,
                TestContext.Current.CancellationToken);
            await quarantine.DeleteMovedAsync(static _ => Task.CompletedTask,
                TestContext.Current.CancellationToken);

            Assert.False(Directory.Exists(root));
            Assert.False(Directory.Exists(destination));
        }
        finally
        {
            if (Directory.Exists(root))
                await WindowsProcessTestDirectory.DeleteAsync(root);
            if (Directory.Exists(quarantineRoot))
                await WindowsProcessTestDirectory.DeleteAsync(quarantineRoot);
        }
    }

    [Fact]
    public async Task NestedAuthorizedRootsRemainPinnedInRequestedOrder()
    {
        var container = Path.Combine(Path.GetTempPath(), "MossAgent.Tests",
            Guid.NewGuid().ToString("N"));
        var root = Path.Combine(container, "worktree");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "owned.txt"), "owned",
            TestContext.Current.CancellationToken);
        try
        {
            var factory = new WindowsRootIdentitySessionFactory();
            using var identity = await factory.OpenAsync(
                [root, container], TestContext.Current.CancellationToken);

            Assert.Equal([root, container], identity.Roots.Select(item => item.Path));
            Assert.IsAssignableFrom<IPathQuarantineSessionFactory>(identity);
            Assert.Throws<NotSupportedException>(() =>
                ((IPathQuarantineSessionFactory)identity).Create(root,
                    Path.Combine(container, "quarantine"),
                    [container], TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(container))
                await WindowsProcessTestDirectory.DeleteAsync(container);
        }
    }
}
