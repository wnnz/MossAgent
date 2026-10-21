using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.Tests;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class DeleteHandleRaceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ParentChangedToJunctionAfterPathCheckIsRefusedWithoutFallback()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var root = fixture.DirectoryFor("authorized");
        var parent = fixture.DirectoryFor("authorized/parent");
        var outside = fixture.DirectoryFor("outside");
        var original = fixture.FileFor(parent);
        var sentinel = fixture.FileFor(outside);
        var renamed = parent + "-saved";
        var sessions = new CallbackPathDeleteSessions { BeforePrepare = () =>
        {
            Directory.Move(parent, renamed);
            TestDirectoryJunction.CreateAsync(parent, outside, Token).GetAwaiter().GetResult();
        } };
        try
        {
            var executor = new ToolExecutor(new([new PathDeleteTool(new(), sessions)]),
                new PolicyApprovalService(static (_, _, _) => ValueTask.FromResult(true)));
            var request = new ToolRequest("substituted-parent", "filesystem.delete_path", JsonSerializer.SerializeToElement(new { path = original }));
            var result = await executor.ExecuteAsync(request,
                new(Guid.NewGuid(), root, [root], ApprovalPolicy.AskEveryTime), Token);
            Assert.Equal("file_delete_unsafe", result.ErrorCode);
            Assert.Equal("handle fixture", await File.ReadAllTextAsync(sentinel, Token));
            Assert.Equal("handle fixture", await File.ReadAllTextAsync(Path.Combine(renamed, "file.txt"), Token));
        }
        finally { if (Directory.Exists(parent) && new DirectoryInfo(parent).LinkTarget is not null) Directory.Delete(parent); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyDirectoryOrReadonlyFileAreHandledWithoutFollowingLinks(bool readOnly)
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var path = readOnly ? fixture.FileFor(fixture.Root) : fixture.DirectoryFor("empty");
        if (readOnly) File.SetAttributes(path, FileAttributes.ReadOnly);
        try
        {
            var executor = new ToolExecutor(new([new PathDeleteTool(new(), new MossAgent.Platform.Windows.Files.WindowsPathDeleteSessionFactory())]),
                new PolicyApprovalService(static (_, _, _) => ValueTask.FromResult(true)));
            var result = await executor.ExecuteAsync(new("empty-or-readonly", "filesystem.delete_path", JsonSerializer.SerializeToElement(new { path })),
                new(Guid.NewGuid(), fixture.Root, [fixture.Root], ApprovalPolicy.AskEveryTime), Token);
            Assert.Equal(!readOnly, result.IsSuccess);
            Assert.Equal(readOnly, Path.Exists(path));
            if (readOnly) Assert.Equal("file_delete_unsafe", result.ErrorCode);
        }
        finally { if (readOnly && File.Exists(path)) File.SetAttributes(path, FileAttributes.Normal); }
    }
}
