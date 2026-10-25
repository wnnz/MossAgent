using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Browser;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class BrowserTakeoverErrorMappingTests
{
    [Theory]
    [InlineData(BrowserControlState.HandedToUser, "browser_takeover_active")]
    [InlineData(BrowserControlState.ReturningToAutomated, "browser_takeover_active")]
    [InlineData(BrowserControlState.Disconnected, "browser_session_disconnected")]
    public async Task TakeoverException_MapsToItsErrorCode(
        BrowserControlState state,
        string expectedCode)
    {
        var tool = new BrowserExceptionProbeTool(
            new BrowserTakeoverException("blocked", state));
        var executor = new ToolExecutor(
            new ToolRegistry([tool]),
            new PolicyApprovalService(static (_, _, _) => ValueTask.FromResult(true)));
        var context = new ToolExecutionContext(
            Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.FullAccess);
        var request = new ToolRequest(
            "call-takeover", tool.Descriptor.Name,
            JsonDocument.Parse("{}").RootElement.Clone());

        var result = await executor.ExecuteAsync(
            request, context, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Equal("blocked", result.Summary);
    }

    [Fact]
    public async Task ProfileDeniedException_MapsToBrowserProfileDenied()
    {
        var tool = new BrowserExceptionProbeTool(
            new BrowserProfileAccessDeniedException("denied"));
        var executor = new ToolExecutor(
            new ToolRegistry([tool]),
            new PolicyApprovalService(static (_, _, _) => ValueTask.FromResult(true)));
        var context = new ToolExecutionContext(
            Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.FullAccess);
        var request = new ToolRequest(
            "call-profile-denied", tool.Descriptor.Name,
            JsonDocument.Parse("{}").RootElement.Clone());

        var result = await executor.ExecuteAsync(
            request, context, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("browser_profile_denied", result.ErrorCode);
        Assert.Equal("denied", result.Summary);
    }
}
