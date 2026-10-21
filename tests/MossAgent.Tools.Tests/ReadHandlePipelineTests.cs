using MossAgent.Domain;
using MossAgent.Testing;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class ReadHandlePipelineTests
{
    [Theory]
    [InlineData("readonly", "capability_denied")]
    [InlineData("approval", "approval_denied")]
    [InlineData("cancel", "tool_cancelled")]
    [InlineData("rootless", "path_denied")]
    public async Task EarlyRefusal_DoesNotOpenAReadHandle(string scenario, string code)
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");
        var guard = new ReadHandleProbe();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        if (scenario == "cancel") cancellation.Cancel();
        var executor = new ToolExecutor(new([fixture.Write]),
            new PolicyApprovalService(static (_, _, _) => ValueTask.FromResult(false)), readHandleValidator: guard);
        var context = fixture.Context with
        {
            ApprovalPolicy = scenario == "readonly" ? ApprovalPolicy.ReadOnly : ApprovalPolicy.AskEveryTime,
            AuthorizedRoots = scenario == "rootless" ? [] : [fixture.Root]
        };
        var result = await executor.ExecuteAsync(FileEditingFixture.Request(fixture.Write,
            new { path = fixture.Path, content = "modified" }), context, cancellation.Token);
        Assert.Equal(code, result.ErrorCode);
        Assert.Equal(0, guard.Calls);
        Assert.Equal("original", await fixture.ContentsAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAndDeadlineAtHandleValidation_ReturnNoContentAndDispose(bool deadline)
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("must not return");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var time = new ManualToolTimeProvider();
        var guard = new ReadHandleProbe { Check = (stream, _) =>
        {
            Assert.Equal(0, stream.Position);
            if (deadline) time.Advance(TimeSpan.FromSeconds(2));
            else cancellation.Cancel();
        } };
        var executor = new ToolExecutor(new([fixture.Read]),
            new PolicyApprovalService(static (_, _, _) => ValueTask.FromResult(true)),
            timeProvider: time, readHandleValidator: guard);
        var result = await executor.ExecuteAsync(FileEditingFixture.Request(fixture.Read, new { path = fixture.Path }),
            fixture.Context with { Timeout = TimeSpan.FromSeconds(1) }, cancellation.Token);
        Assert.Equal(deadline ? "tool_timeout" : "tool_cancelled", result.ErrorCode);
        Assert.Null(result.Content);
        Assert.False(guard.Stream!.CanRead);
        Assert.Null(fixture.Context.FileSnapshots.Get(fixture.Context.TaskId,
            MossAgent.Tools.BuiltIn.Files.FileSnapshotReader.CacheKey(fixture.Path)));
    }

    [Fact]
    public async Task GuardedRead_StillUsesOutputLimits()
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync(new string('x', 4000));
        var guard = new ReadHandleProbe();
        var result = await fixture.ExecuteAsync(fixture.Read, new { path = fixture.Path },
            fixture.Context with { MaximumOutputCharacters = 128, ReadHandleValidator = guard });
        Assert.True(result.IsSuccess, result.Summary);
        Assert.True(result.OutputCharacterCount <= 128);
        Assert.Equal(1, guard.Calls);
        Assert.False(guard.Stream!.CanRead);
    }

    [Theory]
    [InlineData("clear")]
    [InlineData("replace")]
    [InlineData("append")]
    public async Task ChangedRootsDuringApproval_AreRefusedBeforeOpeningOrWriting(string change)
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");
        var roots = new List<string> { fixture.Root };
        var guard = new ReadHandleProbe();
        var executor = new ToolExecutor(new([fixture.Write]), new PolicyApprovalService((_, _, _) =>
        {
            if (change == "clear") roots.Clear();
            else if (change == "replace") roots[0] = Path.GetTempPath();
            else roots.Add(Path.GetTempPath());
            return ValueTask.FromResult(true);
        }), readHandleValidator: guard);
        var result = await executor.ExecuteAsync(FileEditingFixture.Request(fixture.Write,
            new { path = fixture.Path, content = "modified" }), fixture.Context with
            { AuthorizedRoots = roots, ApprovalPolicy = ApprovalPolicy.AskEveryTime }, fixture.Token);
        Assert.Equal("path_denied", result.ErrorCode);
        Assert.Equal(0, guard.Calls);
        Assert.Equal("original", await fixture.ContentsAsync());
        fixture.AssertNoTemporaryFiles();
    }
}
