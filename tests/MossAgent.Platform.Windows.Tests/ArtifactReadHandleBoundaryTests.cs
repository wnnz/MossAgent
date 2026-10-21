using System.Security.Cryptography;
using System.Text;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Tests;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class ArtifactReadHandleBoundaryTests
{
    [Fact]
    public async Task ExistingReadSharingStillPreventsParentRenameAfterOpen()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var paths = new TaskArtifactPaths(new AppDataPaths(fixture.Root));
        var task = Guid.NewGuid();
        var root = Directory.CreateDirectory(paths.GetTaskDirectory(task)).FullName;
        var parent = Directory.CreateDirectory(Path.Combine(root, "nested")).FullName;
        var bytes = Encoding.UTF8.GetBytes("artifact content");
        var path = Path.Combine(parent, "file.snapshot");
        await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);
        var probe = new ReadHandleProbe { Check = (stream, roots) =>
        {
            Assert.Equal(root, Assert.Single(roots));
            Assert.Equal(0, stream.Position);
            Assert.Throws<IOException>(() => Directory.Move(parent, Path.Combine(fixture.Root, "outside")));
            new WindowsFileReadHandleValidator().Validate(stream, roots);
        } };
        var artifact = new ToolArtifact("file.txt", path, "text/plain", bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
        Assert.Equal(bytes, await new TaskArtifactContentReader(paths, probe).ReadAsync(task, artifact, TestContext.Current.CancellationToken));
        Assert.False(probe.Stream!.CanRead);
    }

    [Fact]
    public async Task RestoredOrdinaryPathDoesNotAuthorizePreviouslyOpenedForeignHandle()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var paths = new TaskArtifactPaths(new AppDataPaths(fixture.Root));
        var task = Guid.NewGuid();
        var root = Directory.CreateDirectory(paths.GetTaskDirectory(task)).FullName;
        var outside = fixture.DirectoryFor("outside");
        var foreignPath = fixture.FileFor(outside, "file.snapshot");
        var link = Path.Combine(root, "nested");
        var requestedPath = Path.Combine(link, "file.snapshot");
        var token = TestContext.Current.CancellationToken;
        await TestDirectoryJunction.CreateAsync(link, outside, token);
        try
        {
            using var foreign = new FileStream(requestedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            Directory.Delete(link);
            Directory.CreateDirectory(link);
            var bytes = Encoding.UTF8.GetBytes("new safe artifact");
            await File.WriteAllBytesAsync(requestedPath, bytes, token);
            var artifact = new ToolArtifact("file.txt", requestedPath, "text/plain", bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
            Assert.Equal(bytes, await new TaskArtifactContentReader(paths, new WindowsFileReadHandleValidator()).ReadAsync(task, artifact, token));
            Assert.Equal(0, foreign.Position);
            Assert.Throws<UnauthorizedAccessException>(() => new WindowsFileReadHandleValidator().Validate(foreign, [root]));
            Assert.Equal(0, foreign.Position);
            using var reader = new StreamReader(foreign, leaveOpen: true);
            Assert.Equal("handle fixture", await reader.ReadToEndAsync(token));
            Assert.Equal("handle fixture", await File.ReadAllTextAsync(foreignPath, token));
        }
        finally { if (Directory.Exists(link) && new DirectoryInfo(link).LinkTarget is not null) Directory.Delete(link); }
    }
}
