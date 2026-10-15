using System.Text.Json;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ApprovalRequestIdentityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleAndMissingDecisions_CannotResolveTheNextRequest(bool cancelFirst)
    {
        var viewModel = new ApprovalViewModel();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var descriptor = Descriptor("filesystem.write_file");
        var first = viewModel.RequestAsync(descriptor, Request(descriptor), cancellation.Token).AsTask();
        var previousId = viewModel.CurrentRequestId;
        var second = viewModel.RequestAsync(descriptor, Request(descriptor), TestContext.Current.CancellationToken).AsTask();
        Assert.Equal(2, viewModel.PendingCount);
        Assert.True(viewModel.HasQueuedRequests);
        if (cancelFirst)
        {
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        }
        else
        {
            viewModel.ApproveCommand.Execute(previousId);
            Assert.True(await first);
        }
        Assert.NotEqual(previousId, viewModel.CurrentRequestId);
        Assert.False(viewModel.ApproveCommand.CanExecute(previousId));
        Assert.False(viewModel.DenyCommand.CanExecute(previousId));
        Assert.False(viewModel.ApproveCommand.CanExecute(null));
        viewModel.ApproveCommand.Execute(previousId);
        viewModel.DenyCommand.Execute(previousId);
        viewModel.ApproveCommand.Execute(null);
        viewModel.DenyCommand.Execute(null);

        Assert.False(second.IsCompleted);
        Assert.Equal(1, viewModel.PendingCount);
        Assert.False(viewModel.HasQueuedRequests);
        Assert.Equal(string.Empty, viewModel.QueueLabel);
        viewModel.DenyCommand.Execute(viewModel.CurrentRequestId);
        Assert.False(await second);
        Assert.False(viewModel.IsPending);
        Assert.Equal(0, viewModel.PendingCount);
        Assert.Null(viewModel.CurrentRequestId);
    }

    [Fact]
    public async Task TaskContextAndSafeSummary_FollowTheActiveRequest()
    {
        var viewModel = new ApprovalViewModel();
        var descriptor = Descriptor("filesystem.write_file");
        var context = new ToolExecutionContext(Guid.NewGuid(), @"C:\fixture", [@"C:\fixture"], ApprovalPolicy.AskEveryTime);
        var first = viewModel.RequestAsync(descriptor, new ToolRequest("first", descriptor.Name,
            JsonSerializer.SerializeToElement(new { path = @"C:\fixture\first.txt", content = "private body marker" })),
            context, TestContext.Current.CancellationToken);
        var other = new ToolExecutionContext(Guid.NewGuid(), @"C:\other", [@"C:\other"], ApprovalPolicy.AskEveryTime);
        var second = viewModel.RequestAsync(descriptor, Request(descriptor), other, TestContext.Current.CancellationToken);

        Assert.Equal(context.WorkingDirectory, viewModel.WorkingDirectory);
        Assert.Contains(context.TaskId.ToString(), viewModel.TaskLabel, StringComparison.Ordinal);
        Assert.Contains("first.txt", viewModel.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("private body marker", viewModel.Summary, StringComparison.Ordinal);
        viewModel.ApproveCommand.Execute(viewModel.CurrentRequestId);
        Assert.True(await first);
        Assert.Equal(other.WorkingDirectory, viewModel.WorkingDirectory);
        Assert.Contains(other.TaskId.ToString(), viewModel.TaskLabel, StringComparison.Ordinal);
        Assert.DoesNotContain(context.TaskId.ToString(), viewModel.TaskLabel, StringComparison.Ordinal);
        viewModel.DenyCommand.Execute(viewModel.CurrentRequestId);
        Assert.False(await second);
        Assert.Equal(string.Empty, viewModel.Summary);
        Assert.Equal(string.Empty, viewModel.WorkingDirectory);
    }

    private static ToolDescriptor Descriptor(string name) => new(name, "approval fixture", "{}",
        ToolRiskLevel.Mutation, ToolCapability.FileWrite);

    private static ToolRequest Request(ToolDescriptor descriptor) => new(Guid.NewGuid().ToString("N"), descriptor.Name,
        JsonSerializer.SerializeToElement(new { path = @"C:\other\file.txt", content = "other private body" }));
}
