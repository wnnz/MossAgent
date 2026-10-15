using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class EmbeddedBrowserApprovalPresentationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task PendingApproval_BlocksPresentationUntilResolved(bool approved) => RunAsync(async services =>
    {
        var window = services.GetRequiredService<MainWindowViewModel>();
        var host = window.EmbeddedBrowsers.Host;
        Assert.False(host.IsPresentationBlocked);
        var pending = Request(window.Approval, "first", TestContext.Current.CancellationToken);

        Assert.True(host.IsPresentationBlocked);
        var command = approved ? window.Approval.ApproveCommand : window.Approval.DenyCommand;
        command.Execute(window.Approval.CurrentRequestId);
        Assert.False(host.IsPresentationBlocked);
        Assert.Equal(approved, await pending);
    });

    [Fact]
    public Task CancelledApproval_RestoresPresentation() => RunAsync(async services =>
    {
        var window = services.GetRequiredService<MainWindowViewModel>();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pending = Request(window.Approval, "cancel", cancellation.Token);
        Assert.True(window.EmbeddedBrowsers.Host.IsPresentationBlocked);

        cancellation.Cancel();
        Assert.False(window.EmbeddedBrowsers.Host.IsPresentationBlocked);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    });

    [Fact]
    public Task QueuedApprovals_KeepPresentationBlockedBetweenRequests() => RunAsync(async services =>
    {
        var window = services.GetRequiredService<MainWindowViewModel>();
        var first = Request(window.Approval, "first", TestContext.Current.CancellationToken);
        var second = Request(window.Approval, "second", TestContext.Current.CancellationToken);
        window.Approval.DenyCommand.Execute(window.Approval.CurrentRequestId);

        Assert.True(window.EmbeddedBrowsers.Host.IsPresentationBlocked);
        window.Approval.DenyCommand.Execute(window.Approval.CurrentRequestId);
        Assert.False(window.EmbeddedBrowsers.Host.IsPresentationBlocked);
        Assert.False(await first);
        Assert.False(await second);
    });

    [Fact]
    public Task ExistingApproval_IsBlockedWhenWindowIsCreated() => RunAsync(async services =>
    {
        var approval = services.GetRequiredService<ApprovalViewModel>();
        var pending = Request(approval, "existing", TestContext.Current.CancellationToken);
        var window = services.GetRequiredService<MainWindowViewModel>();

        Assert.True(window.EmbeddedBrowsers.Host.IsPresentationBlocked);
        approval.DenyCommand.Execute(approval.CurrentRequestId);
        Assert.False(window.EmbeddedBrowsers.Host.IsPresentationBlocked);
        Assert.False(await pending);
    });

    [Fact]
    public Task DisposedWindow_UnsubscribesWithoutTouchingUiState() => RunAsync(async services =>
    {
        var window = services.GetRequiredService<MainWindowViewModel>();
        var presentationChanges = 0;
        window.EmbeddedBrowsers.Host.PropertyChanged += (_, _) => presentationChanges++;
        var pending = Request(window.Approval, "dispose", TestContext.Current.CancellationToken);
        Assert.True(window.EmbeddedBrowsers.Host.IsPresentationBlocked);
        var initialChanges = presentationChanges;

        await Task.Run(window.Dispose, TestContext.Current.CancellationToken);
        window.Dispose();
        window.Approval.DenyCommand.Execute(window.Approval.CurrentRequestId);
        Assert.False(await pending);
        Assert.Equal(initialChanges, presentationChanges);
    });

    private static Task<bool> Request(ApprovalViewModel approval, string callId, CancellationToken cancellationToken)
    {
        var descriptor = new ToolDescriptor("filesystem.write_file", "fixture", "{}",
            ToolRiskLevel.Mutation, ToolCapability.FileWrite);
        return approval.RequestAsync(descriptor,
            new ToolRequest(callId, descriptor.Name, JsonSerializer.SerializeToElement(new { })), cancellationToken).AsTask();
    }

    private static async Task RunAsync(Func<ServiceProvider, Task> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var services = AppComposition.CreateServices(root);
            await action(services);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }
}
