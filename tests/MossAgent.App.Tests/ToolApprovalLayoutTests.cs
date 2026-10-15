using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ToolApprovalLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(980, 700, false)]
    [InlineData(980, 700, true)]
    [InlineData(980, 560, false)]
    [InlineData(980, 560, true)]
    [InlineData(480, 640, false)]
    [InlineData(480, 640, true)]
    [InlineData(1280, 820, false)]
    [InlineData(1280, 820, true)]
    public Task ExpandedLongRequest_KeepsCardAndDecisionButtonsInsideViewport(double width, double height, bool dark) =>
        WithWindow(width, height, dark, (window, view, approval, first) =>
        {
            AssertInsideViewport(Card(view), view);
            AssertInsideViewport(Decision(view, approval, true), view);
            AssertInsideViewport(Decision(view, approval, false), view);
            var body = Body(view);
            Assert.True(body.Extent.Height > body.Viewport.Height);
            Assert.Equal(ScrollBarVisibility.Disabled, body.HorizontalScrollBarVisibility);
            Assert.False(first.IsCompleted);
            Assert.Equal(2, approval.PendingCount);
            return Task.CompletedTask;
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ResizingAndScrolling_KeepsDecisionsFixedAndReachable(bool dark) =>
        WithWindow(980, 820, dark, (window, view, approval, first) =>
        {
            window.Height = 560;
            window.Width = 480;
            window.UpdateLayout();
            var approve = Decision(view, approval, true);
            var deny = Decision(view, approval, false);
            var beforeApprove = Bounds(approve, view);
            var beforeDeny = Bounds(deny, view);
            var body = Body(view);
            body.ScrollToEnd();
            window.UpdateLayout();
            Assert.True(body.Offset.Y > 0);
            Assert.Equal(beforeApprove, Bounds(approve, view));
            Assert.Equal(beforeDeny, Bounds(deny, view));
            AssertInsideViewport(Card(view), view);
            AssertInsideViewport(approve, view);
            AssertInsideViewport(deny, view);
            Assert.False(first.IsCompleted);
            return Task.CompletedTask;
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualDenyButton_AdvancesQueueWithoutApprovingTheNextRequest(bool dark) =>
        WithWindow(980, 700, dark, async (window, view, approval, first) =>
        {
            var previous = approval.CurrentRequestId;
            var deny = Decision(view, approval, false);
            Assert.Equal(previous, deny.CommandParameter);
            Assert.True(deny.Focus());
            deny.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
            deny.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
            Assert.False(await first);
            window.UpdateLayout();
            Assert.True(approval.IsPending);
            Assert.Equal(1, approval.PendingCount);
            Assert.NotEqual(previous, approval.CurrentRequestId);
            Assert.Equal(approval.CurrentRequestId, deny.CommandParameter);
            Assert.False(approval.ApproveCommand.CanExecute(previous));
            Assert.False(approval.ContentPreview!.IsOpen);
            AssertInsideViewport(deny, view);
        });

    private Task WithWindow(double width, double height, bool dark,
        Func<AppWindow, ToolApprovalView, ApprovalViewModel, Task<bool>, Task> action) => _session.Dispatch<bool>(async () =>
    {
        var approval = new ApprovalViewModel();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var context = new ToolExecutionContext(Guid.NewGuid(), @"C:\fixture\" + new string('目', 300),
            [@"C:\fixture"], MossAgent.Domain.ApprovalPolicy.AskEveryTime);
        var descriptor = new ToolDescriptor("filesystem.replace_text", new string('说', 500), "{}",
            ToolRiskLevel.Mutation, ToolCapability.FileWrite);
        var request = new ToolRequest("long-request", descriptor.Name, JsonSerializer.SerializeToElement(new
        {
            path = @"C:\fixture\" + new string('路', 500) + ".txt",
            oldText = new string('原', 8000),
            newText = new string('新', 8000)
        }));
        var first = approval.RequestAsync(descriptor, request, context, cancellation.Token).AsTask();
        var second = approval.RequestAsync(descriptor, request, context, cancellation.Token).AsTask();
        approval.ContentPreview!.ToggleCommand.Execute(null);
        var view = new ToolApprovalView { DataContext = approval };
        var window = new AppWindow
        {
            Width = width,
            Height = height,
            Content = view,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            await action(window, view, approval, first);
        }
        finally
        {
            cancellation.Cancel();
            foreach (var pending in new[] { first, second })
            {
                try { await pending; }
                catch (OperationCanceledException) { }
            }
            window.Close();
        }
        return true;
    }, TestContext.Current.CancellationToken);

    private static AppCard Card(ToolApprovalView view) =>
        Assert.Single(view.GetVisualDescendants().OfType<AppCard>(), card => card.Classes.Contains("tool-approval-card"));

    private static AppScrollViewer Body(ToolApprovalView view) =>
        Assert.Single(view.GetVisualDescendants().OfType<AppScrollViewer>(), scroll => scroll.Classes.Contains("approval-body-scroll"));

    private static AppButton Decision(ToolApprovalView view, ApprovalViewModel approval, bool approve) =>
        Assert.Single(view.GetVisualDescendants().OfType<AppButton>(), button => ReferenceEquals(button.Command,
            approve ? approval.ApproveCommand : approval.DenyCommand));

    private static Rect Bounds(Control control, ToolApprovalView view) =>
        new(control.TranslatePoint(default, view)!.Value, control.Bounds.Size);

    private static void AssertInsideViewport(Control control, ToolApprovalView view)
    {
        var bounds = Bounds(control, view);
        Assert.InRange(bounds.Left, 0, view.Bounds.Width);
        Assert.InRange(bounds.Top, 0, view.Bounds.Height);
        Assert.InRange(bounds.Right, bounds.Left, view.Bounds.Width);
        Assert.InRange(bounds.Bottom, bounds.Top, view.Bounds.Height);
    }
}
