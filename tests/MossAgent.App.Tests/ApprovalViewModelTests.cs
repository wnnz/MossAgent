using System.Text.Json;
using MossAgent.App.ViewModels;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ApprovalViewModelTests
{
    [Fact]
    public async Task RequestAsync_QueuesConcurrentRequestsInOrder()
    {
        var viewModel = new ApprovalViewModel();

        var first = viewModel.RequestAsync(
            CreateDescriptor("write_file"), CreateRequest("first"), TestContext.Current.CancellationToken);
        var second = viewModel.RequestAsync(
            CreateDescriptor("run_shell"), CreateRequest("second"), TestContext.Current.CancellationToken);

        Assert.True(viewModel.IsPending);
        Assert.Equal("write_file", viewModel.ToolName);
        viewModel.ApproveCommand.Execute(null);
        Assert.True(await first);
        Assert.Equal("run_shell", viewModel.ToolName);

        viewModel.DenyCommand.Execute(null);
        Assert.False(await second);
        Assert.False(viewModel.IsPending);
    }

    [Fact]
    public async Task RequestAsync_CancelledWaitingRequestDoesNotAffectOthers()
    {
        var viewModel = new ApprovalViewModel();
        using var cancellation = new CancellationTokenSource();

        var first = viewModel.RequestAsync(
            CreateDescriptor("write_file"), CreateRequest("first"), TestContext.Current.CancellationToken);
        var cancelled = viewModel.RequestAsync(
            CreateDescriptor("run_shell"), CreateRequest("cancelled"), cancellation.Token);
        var third = viewModel.RequestAsync(
            CreateDescriptor("browser_click"), CreateRequest("third"), TestContext.Current.CancellationToken);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await cancelled);
        viewModel.ApproveCommand.Execute(null);
        Assert.True(await first);
        Assert.Equal("browser_click", viewModel.ToolName);

        viewModel.ApproveCommand.Execute(null);
        Assert.True(await third);
        Assert.False(viewModel.IsPending);
    }

    [Fact]
    public async Task RequestAsync_CancelledCurrentRequestAdvancesQueue()
    {
        var viewModel = new ApprovalViewModel();
        using var cancellation = new CancellationTokenSource();

        var first = viewModel.RequestAsync(
            CreateDescriptor("write_file"), CreateRequest("first"), cancellation.Token);
        var second = viewModel.RequestAsync(
            CreateDescriptor("run_shell"), CreateRequest("second"), TestContext.Current.CancellationToken);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await first);
        Assert.Equal("run_shell", viewModel.ToolName);

        viewModel.ApproveCommand.Execute(null);
        Assert.True(await second);
        Assert.False(viewModel.IsPending);
    }

    [Fact]
    public async Task RequestAsync_ProcessesLargeQueueWithoutLosingResults()
    {
        var viewModel = new ApprovalViewModel();
        var results = Enumerable.Range(0, 128)
            .Select(index => viewModel.RequestAsync(
                CreateDescriptor($"tool_{index}"),
                CreateRequest($"call_{index}"),
                TestContext.Current.CancellationToken).AsTask())
            .ToArray();

        for (var index = 0; index < results.Length; index++)
        {
            var approved = index % 2 == 0;
            Assert.Equal($"tool_{index}", viewModel.ToolName);
            (approved ? viewModel.ApproveCommand : viewModel.DenyCommand).Execute(null);
            Assert.Equal(approved, await results[index]);
        }

        Assert.False(viewModel.IsPending);
    }

    private static ToolDescriptor CreateDescriptor(string name) =>
        new(name, $"Approve {name}", "{}", ToolRiskLevel.Mutation, ToolCapability.FileWrite);

    private static ToolRequest CreateRequest(string callId) =>
        new(callId, "unused", JsonDocument.Parse("{}").RootElement.Clone());
}
