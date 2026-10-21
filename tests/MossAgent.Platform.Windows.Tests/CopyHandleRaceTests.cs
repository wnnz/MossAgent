using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.Tests;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class CopyHandleRaceTests
{
    [Fact]
    public async Task SourceParentSwapAfterValidationIsRefusedBeforeAnyDestinationCreation()
    {
        CopyHandleFixture? fixture = null;
        var sessions = new CallbackFileTreeSessions { BeforePrepare = () =>
        {
            Directory.Move(fixture!.SourceDirectory, fixture.SourceDirectory + "-saved");
            TestDirectoryJunction.CreateAsync(fixture.SourceDirectory, fixture.Outside, fixture.Token).GetAwaiter().GetResult();
        } };
        using var owned = new CopyHandleFixture(sessions);
        fixture = owned;
        var sentinel = Path.Combine(fixture.Outside, "file.txt");
        await File.WriteAllTextAsync(sentinel, "outside source sentinel", fixture.Token);
        try
        {
            var result = await fixture.CopyAsync();
            Assert.Equal("file_tree_unsafe", result.ErrorCode);
            Assert.False(File.Exists(fixture.Destination));
            Assert.Equal("outside source sentinel", await File.ReadAllTextAsync(sentinel, fixture.Token));
        }
        finally { if (new DirectoryInfo(fixture.SourceDirectory).LinkTarget is not null) Directory.Delete(fixture.SourceDirectory); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DestinationParentSwap_LegacyControlWritesOutsideNativeCompositionRefuses(bool guarded)
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var token = TestContext.Current.CancellationToken;
        var root = fixture.DirectoryFor("authorized");
        var parent = fixture.DirectoryFor("authorized/target");
        var outside = fixture.DirectoryFor("outside");
        var source = fixture.FileFor(root, "source.txt");
        var sentinel = fixture.FileFor(outside, "copied.txt");
        await File.WriteAllTextAsync(source, "copy source sentinel", token);
        var validator = new CallbackReadHandleValidator(() =>
        {
            Directory.Move(parent, parent + "-saved");
            TestDirectoryJunction.CreateAsync(parent, outside, token).GetAwaiter().GetResult();
        });
        try
        {
            var copy = guarded
                ? new PathCopyTool(new(), new(new(), new WindowsAtomicFileWriteSessionFactory()),
                    new WindowsFileTreeReadSessionFactory(), new WindowsAuthorizedDirectoryCreator())
                : new PathCopyTool(new());
            var executor = new ToolExecutor(new([copy]),
                new PolicyApprovalService(static (_, _, _) => ValueTask.FromResult(true)), readHandleValidator: validator);
            var result = await Task.Run(() => executor.ExecuteAsync(new("copy-race", "filesystem.copy_path",
                JsonSerializer.SerializeToElement(new { source, destination = Path.Combine(parent, "copied.txt"), overwrite = true })),
                new(Guid.NewGuid(), root, [root], ApprovalPolicy.FullAccess), token), token);
            Assert.Equal(!guarded, result.IsSuccess);
            if (guarded) Assert.Equal("path_denied", result.ErrorCode);
            Assert.Equal(guarded ? "handle fixture" : "copy source sentinel", await File.ReadAllTextAsync(sentinel, token));
        }
        finally { if (Directory.Exists(parent) && new DirectoryInfo(parent).LinkTarget is not null) Directory.Delete(parent); }
    }
}
