using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.Tests;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class DirectoryHandleTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreatesAllMissingComponentsCountsAndReusesExistingAuthorizedRoot()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var path = Path.Combine(fixture.Root, "one", "two", "three");
        var creator = new WindowsAuthorizedDirectoryCreator();
        Assert.Equal(3, await creator.CreateAsync(path, [fixture.Root], _ => Task.CompletedTask, Token));
        Assert.Equal(0, await creator.CreateAsync(path, [fixture.Root], _ => Task.CompletedTask, Token));
        Assert.Equal(0, await creator.CreateAsync(fixture.Root, [fixture.Root], _ => Task.CompletedTask, Token));
        Assert.True(Directory.Exists(path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TraversalOrJunctionNeverCreatesOutsideAuthorizedRoot(bool link)
    {
        using var fixture = new CopyHandleFixture();
        var parent = Path.Combine(fixture.Authorized, "junction");
        if (link) await TestDirectoryJunction.CreateAsync(parent, fixture.Outside, Token);
        try
        {
            var target = link ? Path.Combine(parent, "new") : Path.Combine(fixture.Authorized, "..", "outside", "new");
            var error = await Assert.ThrowsAsync<FileMutationSessionException>(() => new WindowsAuthorizedDirectoryCreator()
                .CreateAsync(target, [fixture.Authorized], _ => Task.CompletedTask, Token));
            Assert.False(error.HasSideEffects);
            Assert.False(Directory.Exists(Path.Combine(fixture.Outside, "new")));
        }
        finally { if (link) Directory.Delete(parent); }
    }

    [Fact]
    public async Task PinnedParentCannotBeRenamedDuringFurtherCreation()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var first = Path.Combine(fixture.Root, "one");
        var checks = 0;
        var count = await new WindowsAuthorizedDirectoryCreator().CreateAsync(Path.Combine(first, "two"), [fixture.Root], _ =>
        {
            if (++checks == 2) Assert.ThrowsAny<IOException>(() => Directory.Move(first, first + ".moved"));
            return Task.CompletedTask;
        }, Token);
        Assert.Equal(2, count);
        Assert.True(Directory.Exists(Path.Combine(first, "two")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PipelineCancellationOrTimeoutAfterOneDirectoryKeepsAccurateSideEffectMessage(bool deadline)
    {
        using var fixture = new ReadHandleDirectoryFixture();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var time = new MossAgent.Testing.ManualToolTimeProvider();
        var first = Path.Combine(fixture.Root, "one");
        var path = Path.Combine(first, "two");
        var context = new ToolExecutionContext(Guid.NewGuid(), fixture.Root, [fixture.Root], ApprovalPolicy.FullAccess,
            Timeout: TimeSpan.FromSeconds(1)) { WorkspaceGuard = new CallbackCopyWorkspaceGuard(() =>
            {
                if (!Directory.Exists(first)) return;
                if (deadline) time.Advance(TimeSpan.FromSeconds(2));
                else cancellation.Cancel();
            }) };
        var tool = new DirectoryCreateTool(new(), new WindowsAuthorizedDirectoryCreator());
        var executor = new ToolExecutor(new([tool]), new PolicyApprovalService(static (_, _, _) => ValueTask.FromResult(true)), timeProvider: time);
        var result = await executor.ExecuteAsync(new("directory-interrupted", tool.Descriptor.Name,
            JsonSerializer.SerializeToElement(new { path })), context, cancellation.Token);
        Assert.Equal(deadline ? "tool_timeout" : "tool_cancelled", result.ErrorCode);
        Assert.Contains("已新建 1", result.Summary);
        Assert.True(Directory.Exists(first));
        Assert.False(Directory.Exists(path));
    }
}
