using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class ReadHandleGuardTests
{
    [Theory]
    [InlineData("read")]
    [InlineData("preview")]
    [InlineData("write")]
    [InlineData("replace")]
    [InlineData("copy")]
    public async Task DeniedOpenedHandle_IsDisposedWithoutReadingOrChangingTheFile(string operation)
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("alpha");
        var guard = new ReadHandleProbe { Check = (stream, _) =>
        {
            Assert.Equal(0, stream.Position);
            throw new UnauthorizedAccessException("handle scope refused");
        } };
        var tool = Tool(fixture, operation);
        var executor = new ToolExecutor(new([tool]), Approval(), readHandleValidator: guard);
        var result = await executor.ExecuteAsync(FileEditingFixture.Request(tool, Arguments(fixture, operation)),
            fixture.Context with { ArtifactDirectory = Path.Combine(fixture.Root, "artifacts") }, fixture.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal("path_denied", result.ErrorCode);
        Assert.Null(result.Content);
        Assert.Equal(1, guard.Calls);
        Assert.False(guard.Stream!.CanRead);
        Assert.Null(fixture.Context.FileSnapshots.Get(fixture.Context.TaskId, FileSnapshotReader.CacheKey(fixture.Path)));
        Assert.Equal("alpha", await fixture.ContentsAsync());
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "artifacts")));
        fixture.AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task MissingFileWrite_DoesNotRequireAnExistingReadHandle()
    {
        using var fixture = new FileEditingFixture();
        var guard = new ReadHandleProbe();
        var executor = new ToolExecutor(new([fixture.Write]), Approval(), readHandleValidator: guard);
        var result = await executor.ExecuteAsync(FileEditingFixture.Request(fixture.Write,
            new { path = fixture.Path, content = "created" }), fixture.Context, fixture.Token);
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal(0, guard.Calls);
        Assert.Equal("created", await fixture.ContentsAsync());
    }

    [Fact]
    public async Task HandleValidation_RejectsChangedRootsAndUsesTheExecutorValidator()
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("alpha");
        var roots = new[] { fixture.Root };
        var untrusted = new ReadHandleProbe { Check = (_, _) => throw new InvalidOperationException() };
        var guard = new ReadHandleProbe { Check = (_, captured) =>
        {
            Assert.Equal(fixture.Root, Assert.Single(captured));
            roots[0] = Path.GetTempPath();
            Assert.Throws<UnauthorizedAccessException>(() => captured[0]);
        } };
        var executor = new ToolExecutor(new([fixture.Read]), Approval(), readHandleValidator: guard);
        var result = await executor.ExecuteAsync(FileEditingFixture.Request(fixture.Read, new { path = fixture.Path }),
            fixture.Context with { AuthorizedRoots = roots, ReadHandleValidator = untrusted }, fixture.Token);
        Assert.Equal("path_denied", result.ErrorCode);
        Assert.Null(result.Content);
        Assert.Equal(1, guard.Calls);
        Assert.Equal(0, untrusted.Calls);
        Assert.False(guard.Stream!.CanRead);
    }

    private static PolicyApprovalService Approval() => new(static (_, _, _) => ValueTask.FromResult(true));

    private static IAgentTool Tool(FileEditingFixture fixture, string operation) => operation switch
    {
        "read" => fixture.Read,
        "preview" => new FilePreviewTool(fixture.Paths),
        "write" => fixture.Write,
        "replace" => fixture.Replace,
        "copy" => new PathCopyTool(fixture.Paths),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    private static object Arguments(FileEditingFixture fixture, string operation) => operation switch
    {
        "write" => new { path = fixture.Path, content = "modified" },
        "replace" => new { path = fixture.Path, oldText = "alpha", newText = "modified" },
        "copy" => new { source = fixture.Path, destination = Path.Combine(fixture.Root, "copied.txt") },
        _ => new { path = fixture.Path }
    };
}
