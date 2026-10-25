using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Tools.Abstractions.Browser;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class BrowserProfileApprovalViewModelTests
{
    [Fact]
    public async Task ConfirmAsync_ApproveResolvesTrue()
    {
        var viewModel = new BrowserProfileApprovalViewModel();
        var taskId = Guid.NewGuid();

        var pending = viewModel.ConfirmAsync(
            taskId, BrowserProfileMode.DailyProfile, CancellationToken.None).AsTask();

        Assert.True(viewModel.IsPending);
        Assert.Equal(ApprovalText.BrowserProfileTitle, viewModel.Title);
        Assert.Equal(ApprovalText.Format("BrowserProfileDaily"), viewModel.Description);
        Assert.Equal(ApprovalText.Format("Task", taskId), viewModel.TaskLabel);

        viewModel.ApproveCommand.Execute(null);

        Assert.True(await pending);
        Assert.False(viewModel.IsPending);
    }

    [Fact]
    public async Task ConfirmAsync_DenyResolvesFalse()
    {
        var viewModel = new BrowserProfileApprovalViewModel();

        var pending = viewModel.ConfirmAsync(
            Guid.NewGuid(), BrowserProfileMode.ExistingCdp, CancellationToken.None).AsTask();

        Assert.Equal(ApprovalText.Format("BrowserProfileCdp"), viewModel.Description);
        viewModel.DenyCommand.Execute(null);

        Assert.False(await pending);
        Assert.False(viewModel.IsPending);
    }

    [Fact]
    public async Task QueuedRequests_ResolveInOrder()
    {
        var viewModel = new BrowserProfileApprovalViewModel();
        var first = viewModel.ConfirmAsync(
            Guid.NewGuid(), BrowserProfileMode.DailyProfile, CancellationToken.None).AsTask();
        var second = viewModel.ConfirmAsync(
            Guid.NewGuid(), BrowserProfileMode.ExistingCdp, CancellationToken.None).AsTask();

        Assert.True(viewModel.HasQueuedRequests);
        viewModel.ApproveCommand.Execute(null);
        Assert.True(await first);
        Assert.True(viewModel.IsPending);
        Assert.Equal(ApprovalText.Format("BrowserProfileCdp"), viewModel.Description);

        viewModel.DenyCommand.Execute(null);
        Assert.False(await second);
        Assert.False(viewModel.IsPending);
    }

    [Fact]
    public async Task CancelledRequest_ThrowsAndAdvancesQueue()
    {
        var viewModel = new BrowserProfileApprovalViewModel();
        using var cancellation = new CancellationTokenSource();
        var first = viewModel.ConfirmAsync(
            Guid.NewGuid(), BrowserProfileMode.DailyProfile, cancellation.Token).AsTask();
        var second = viewModel.ConfirmAsync(
            Guid.NewGuid(), BrowserProfileMode.ExistingCdp, CancellationToken.None).AsTask();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.True(viewModel.IsPending);
        Assert.Equal(ApprovalText.Format("BrowserProfileCdp"), viewModel.Description);
        viewModel.ApproveCommand.Execute(null);
        Assert.True(await second);
    }
}
