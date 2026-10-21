using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class AtomicWritePipelineTests
{
    [Theory]
    [InlineData("readonly", "capability_denied")]
    [InlineData("deny", "approval_denied")]
    [InlineData("rootless", "path_denied")]
    public async Task ApprovalOrCapabilityRefusalNeverCreatesNativeSession(string scenario, string code)
    {
        using var fixture = new AtomicWriteFixture();
        await fixture.SeedAsync();
        var factory = new CallbackAtomicWriteSessions();
        var tool = new FileWriteTool(new(), new(new(), factory));
        var executor = new ToolExecutor(new([tool]), new PolicyApprovalService(static (_, _, _) => ValueTask.FromResult(false)));
        var context = fixture.Context with
        {
            ApprovalPolicy = scenario == "readonly" ? ApprovalPolicy.ReadOnly : ApprovalPolicy.AskEveryTime,
            AuthorizedRoots = scenario == "rootless" ? [] : [fixture.Root]
        };
        var result = await executor.ExecuteAsync(Request(fixture.Path), context, fixture.Token);
        Assert.Equal(code, result.ErrorCode);
        Assert.Equal(0, factory.Creates);
        Assert.Equal("original", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
        fixture.AssertNoTemporaryFiles();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CancellationOrDeadlineAfterAcquisitionClosesSessionWithoutCommit(bool deadline, bool portFailure)
    {
        using var fixture = new AtomicWriteFixture();
        await fixture.SeedAsync();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var time = new ManualToolTimeProvider();
        FileStream? staged = null;
        var factory = new CallbackAtomicWriteSessions(session =>
        {
            staged = session.Stream;
            if (deadline) time.Advance(TimeSpan.FromSeconds(2));
            else cancellation.Cancel();
            if (portFailure) throw new FileWriteSessionException("synthetic safe refusal");
        });
        var tool = new FileWriteTool(new(), new(new(), factory));
        var executor = new ToolExecutor(new([tool]), new PolicyApprovalService(static (_, _, _) => ValueTask.FromResult(true)), timeProvider: time);
        var result = await executor.ExecuteAsync(Request(fixture.Path),
            fixture.Context with { Timeout = TimeSpan.FromSeconds(1) }, cancellation.Token);
        Assert.Equal(deadline ? "tool_timeout" : "tool_cancelled", result.ErrorCode);
        Assert.Equal(1, factory.Creates);
        Assert.NotNull(staged);
        Assert.False(staged.CanRead);
        Assert.Null(result.FileChanges);
        Assert.Equal("original", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
        fixture.AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task NativeSessionSuccessStillUsesOutputLimit()
    {
        using var fixture = new AtomicWriteFixture();
        var tool = new FileWriteTool(new(), fixture.Writer);
        var executor = new ToolExecutor(new([tool]), new PolicyApprovalService(static (_, _, _) => ValueTask.FromResult(true)));
        var result = await executor.ExecuteAsync(Request(fixture.Path), fixture.Context with { MaximumOutputCharacters = 64 }, fixture.Token);
        Assert.True(result.IsSuccess, result.Summary);
        Assert.True(result.OutputCharacterCount <= 64);
        Assert.Equal("updated", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
        fixture.AssertNoTemporaryFiles();
    }

    private static ToolRequest Request(string path) =>
        new(Guid.NewGuid().ToString("N"), "filesystem.write_file", JsonSerializer.SerializeToElement(new { path, content = "updated" }));

    [Fact]
    public async Task UnsupportedMetadataReturnsActionableFailureThroughPipeline()
    {
        using var fixture = new AtomicWriteFixture();
        await fixture.SeedAsync();
        await File.WriteAllTextAsync(fixture.Path + ":metadata", "preserved", fixture.Token);
        var tool = new FileWriteTool(new(), fixture.Writer);
        var executor = new ToolExecutor(new([tool]), new PolicyApprovalService(static (_, _, _) => ValueTask.FromResult(true)));
        var result = await executor.ExecuteAsync(Request(fixture.Path), fixture.Context, fixture.Token);
        Assert.Equal("file_write_metadata_unsupported", result.ErrorCode);
        Assert.Contains("附加数据流", result.Summary);
        Assert.Null(result.FileChanges);
        Assert.Equal("original", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
        fixture.AssertNoTemporaryFiles();
    }
}
