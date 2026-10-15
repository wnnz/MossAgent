using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class FileRevealToolTests
{
    [Fact]
    public async Task ReadOnlyDenied_AskRequiresApproval_InvalidSchemaNeverReachesPlatform()
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("synthetic");
        var service = new ProbeFileRevealService();
        var tool = new FileRevealTool(fixture.Paths, service);
        var executor = new ToolExecutor(new ToolRegistry([tool]), new PreviewDenyApprovalService());
        var request = FileEditingFixture.Request(tool, new { path = fixture.Path });
        var readOnly = await executor.ExecuteAsync(request, fixture.Context with { ApprovalPolicy = ApprovalPolicy.ReadOnly }, fixture.Token);
        Assert.Equal("capability_denied", readOnly.ErrorCode);
        var ask = await executor.ExecuteAsync(request, fixture.Context with { ApprovalPolicy = ApprovalPolicy.AskEveryTime }, fixture.Token);
        Assert.Equal("approval_denied", ask.ErrorCode);
        var invalid = await executor.ExecuteAsync(FileEditingFixture.Request(tool, new { path = fixture.Path, extra = true }), fixture.Context, fixture.Token);
        Assert.Equal("invalid_arguments", invalid.ErrorCode);
        Assert.Equal(ToolRiskLevel.ExternalSideEffect, tool.Descriptor.RiskLevel);
        Assert.Equal(ToolCapability.FileRead | ToolCapability.Process, tool.Descriptor.RequiredCapabilities);
        Assert.Empty(service.Paths);
    }

    [Fact]
    public async Task ApprovedExistingFile_UsesCanonicalPathAndBoundedSafeOutput()
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("Never put this file content in the reveal result.");
        var service = new ProbeFileRevealService();
        var tool = new FileRevealTool(fixture.Paths, service);
        var result = await fixture.ExecuteAsync(tool, new { path = Path.GetRelativePath(fixture.Root, fixture.Path) });
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal(fixture.Path, Assert.Single(service.Paths));
        Assert.DoesNotContain("Never put", result.Summary);
        Assert.Equal(string.Empty, result.Content);
        Assert.Null(result.Artifacts);
    }

    [Theory]
    [InlineData("../escape.txt", "path_denied")]
    [InlineData("missing.txt", "invalid_arguments")]
    [InlineData(".", "invalid_arguments")]
    public async Task OutsideMissingAndDirectoryPaths_AreRejectedBeforeApproval(string path, string error)
    {
        using var fixture = new FileEditingFixture();
        var service = new ProbeFileRevealService();
        var result = await fixture.ExecuteAsync(new FileRevealTool(fixture.Paths, service), new { path });
        Assert.Equal(error, result.ErrorCode);
        Assert.Empty(service.Paths);
    }

    [Fact]
    public async Task CancellationTimeoutAndPlatformFailure_DoNotReturnFalseSuccess()
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("synthetic");
        var service = new ProbeFileRevealService { Reveal = (_, token) => Task.Delay(Timeout.InfiniteTimeSpan, token) };
        var tool = new FileRevealTool(fixture.Paths, service);
        var timeout = await fixture.ExecuteAsync(tool, new { path = fixture.Path }, fixture.Context with { Timeout = TimeSpan.FromMilliseconds(30) });
        Assert.Equal("tool_timeout", timeout.ErrorCode);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        cancellation.Cancel();
        var executor = new ToolExecutor(new ToolRegistry([tool]), new PolicyApprovalService((_, _, _) => ValueTask.FromResult(true)));
        var cancelled = await executor.ExecuteAsync(FileEditingFixture.Request(tool, new { path = fixture.Path }), fixture.Context, cancellation.Token);
        Assert.Equal("tool_cancelled", cancelled.ErrorCode);
        service.Reveal = (_, _) => throw new InvalidOperationException("secret-detail");
        var failed = await fixture.ExecuteAsync(tool, new { path = fixture.Path });
        Assert.Equal("reveal_failed", failed.ErrorCode);
        Assert.DoesNotContain("secret-detail", failed.Summary);
    }
}
