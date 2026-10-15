using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class PolicyApprovalServiceTests
{
    [Fact]
    public async Task ReadOnlyPolicy_DeniesMutation()
    {
        var service = new PolicyApprovalService(
            static (_, _, _) => ValueTask.FromResult(true));
        var descriptor = new ToolDescriptor(
            "test.write", "test", "{}", ToolRiskLevel.Mutation, ToolCapability.FileWrite);
        var request = new ToolRequest("1", "test.write", JsonDocument.Parse("{}").RootElement.Clone());
        var context = new ToolExecutionContext(
            Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.ReadOnly);

        var approved = await service.IsApprovedAsync(descriptor, request, context, CancellationToken.None);

        Assert.False(approved);
    }

    [Fact]
    public async Task AskEveryTime_UsesPromptForMutation()
    {
        var prompted = false;
        var service = new PolicyApprovalService((_, _, _) =>
        {
            prompted = true;
            return ValueTask.FromResult(true);
        });
        var descriptor = new ToolDescriptor(
            "test.write", "test", "{}", ToolRiskLevel.Mutation, ToolCapability.FileWrite);
        var request = new ToolRequest("1", "test.write", JsonDocument.Parse("{}").RootElement.Clone());
        var context = new ToolExecutionContext(
            Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.AskEveryTime);

        var approved = await service.IsApprovedAsync(descriptor, request, context, CancellationToken.None);

        Assert.True(approved);
        Assert.True(prompted);
    }

    [Fact]
    public async Task AskEveryTime_UsesPromptForReadOnlyBrowserInteraction()
    {
        var prompted = false;
        var service = new PolicyApprovalService((_, _, _) =>
        {
            prompted = true;
            return ValueTask.FromResult(true);
        });
        var descriptor = new ToolDescriptor(
            "browser.snapshot", "test", "{}", ToolRiskLevel.ReadOnly, ToolCapability.BrowserRead);
        var request = new ToolRequest("1", "browser.snapshot", JsonDocument.Parse("{}").RootElement.Clone());
        var context = new ToolExecutionContext(
            Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.AskEveryTime);

        var approved = await service.IsApprovedAsync(descriptor, request, context, CancellationToken.None);

        Assert.True(approved);
        Assert.True(prompted);
    }

    [Fact]
    public async Task AskEveryTime_UsesPromptForFileWriteMarkedReadOnly()
    {
        var prompted = false;
        var service = new PolicyApprovalService((_, _, _) =>
        {
            prompted = true;
            return ValueTask.FromResult(true);
        });
        var descriptor = new ToolDescriptor(
            "filesystem.write_file", "test", "{}", ToolRiskLevel.ReadOnly, ToolCapability.FileWrite);
        var request = new ToolRequest(
            "1", "filesystem.write_file", JsonDocument.Parse("{}").RootElement.Clone());
        var context = new ToolExecutionContext(
            Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.AskEveryTime);

        var approved = await service.IsApprovedAsync(descriptor, request, context, CancellationToken.None);

        Assert.True(approved);
        Assert.True(prompted);
    }

    [Fact]
    public async Task ReadOnlyPolicy_DeniesScreenCaptureEvenWhenMarkedReadOnly()
    {
        var service = new PolicyApprovalService(
            static (_, _, _) => ValueTask.FromResult(true));
        var descriptor = new ToolDescriptor(
            "screen.capture", "test", "{}", ToolRiskLevel.ReadOnly, ToolCapability.ScreenCapture);
        var request = new ToolRequest("1", "screen.capture", JsonDocument.Parse("{}").RootElement.Clone());
        var context = new ToolExecutionContext(
            Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.ReadOnly);

        var approved = await service.IsApprovedAsync(descriptor, request, context, CancellationToken.None);

        Assert.False(approved);
    }
}
