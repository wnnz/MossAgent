using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class NavigationDragScrollTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task EdgeScroll_ClampsAndStopsWhenPayloadExpires(bool bottom) => WithViewer(async (window, viewer, child) =>
    {
        var payload = new AppProjectDragPayload(viewer.NavigationDragScope!, new object());
        using var transfer = ProjectNavigationDragTests.Transfer(payload);
        viewer.Offset = new Vector(0, 100);
        ProjectNavigationDragTests.Raise(viewer, DragDrop.DragOverEvent, transfer,
            new Point(60, bottom ? viewer.Viewport.Height - 1 : 1));
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.True(bottom ? viewer.Offset.Y > 100 : viewer.Offset.Y < 100);
        payload.Invalidate();
        var stopped = viewer.Offset;
        await Task.Delay(80, TestContext.Current.CancellationToken);
        Assert.Equal(stopped, viewer.Offset);
        var maximum = viewer.Extent.Height - viewer.Viewport.Height;
        viewer.Offset = new Vector(0, bottom ? maximum - 1 : 1);
        using var next = ProjectNavigationDragTests.Transfer(new AppProjectDragPayload(viewer.NavigationDragScope!, new object()));
        ProjectNavigationDragTests.Raise(viewer, DragDrop.DragOverEvent, next,
            new Point(60, bottom ? viewer.Viewport.Height - 1 : 1));
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.Equal(bottom ? maximum : 0, viewer.Offset.Y);
    });

    [Theory]
    [InlineData("foreign")]
    [InlineData("center")]
    [InlineData("outside")]
    [InlineData("text")]
    public Task ForeignAndNonEdgeDrag_DoNotScroll(string kind) => WithViewer(async (window, viewer, child) =>
    {
        var payload = new AppProjectDragPayload(kind == "foreign" ? new object() : viewer.NavigationDragScope!, new object());
        using var transfer = kind == "text" ? new DataTransfer() : ProjectNavigationDragTests.Transfer(payload);
        if (kind == "text") transfer.Add(DataTransferItem.CreateText("external"));
        viewer.Offset = new Vector(0, 100);
        var position = new Point(kind == "outside" ? -1 : 60, kind == "center" ? viewer.Viewport.Height / 2 : 1);
        ProjectNavigationDragTests.Raise(viewer, DragDrop.DragOverEvent, transfer, position);
        await Task.Delay(80, TestContext.Current.CancellationToken);
        Assert.Equal(100, viewer.Offset.Y);
    });

    [Theory]
    [InlineData("scope")]
    [InlineData("detach")]
    [InlineData("drop")]
    [InlineData("leave")]
    public Task LifetimeAndViewportExit_StopScrolling(string reason) => WithViewer(async (window, viewer, child) =>
    {
        using var transfer = ProjectNavigationDragTests.Transfer(new AppProjectDragPayload(viewer.NavigationDragScope!, new object()));
        viewer.Offset = new Vector(0, 100);
        ProjectNavigationDragTests.Raise(viewer, DragDrop.DragOverEvent, transfer, new Point(60, 1));
        if (reason == "scope") viewer.NavigationDragScope = new object();
        if (reason == "detach") window.Content = null;
        if (reason == "drop") ProjectNavigationDragTests.Raise(child, DragDrop.DropEvent, transfer, default);
        if (reason == "leave") ProjectNavigationDragTests.Raise(viewer, DragDrop.DragLeaveEvent, transfer, new Point(-1, -1));
        var stopped = viewer.Offset;
        await Task.Delay(80, TestContext.Current.CancellationToken);
        Assert.Equal(stopped, viewer.Offset);
        if (reason == "detach") Assert.False(DragDrop.GetAllowDrop(viewer));
    });

    [Fact]
    public Task LeavingOnlyAChildInsideViewport_DoesNotStopEdgeScroll() => WithViewer(async (window, viewer, child) =>
    {
        using var transfer = ProjectNavigationDragTests.Transfer(new AppProjectDragPayload(viewer.NavigationDragScope!, new object()));
        viewer.Offset = new Vector(0, 100);
        ProjectNavigationDragTests.Raise(viewer, DragDrop.DragOverEvent, transfer, new Point(60, 1));
        var insideChild = viewer.TranslatePoint(new Point(60, 1), child)!.Value;
        ProjectNavigationDragTests.Raise(child, DragDrop.DragLeaveEvent, transfer, insideChild);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.True(viewer.Offset.Y < 100);
    });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task UnavailableViewportOrAncestorStopsActiveAndLateDragScroll(bool hidden, bool ancestor) => WithViewer(async (window, viewer, child) =>
    {
        using var transfer = ProjectNavigationDragTests.Transfer(new AppProjectDragPayload(viewer.NavigationDragScope!, new object()));
        viewer.Offset = new Vector(0, 100);
        ProjectNavigationDragTests.Raise(viewer, DragDrop.DragOverEvent, transfer, new Point(60, 1));
        await Task.Delay(80, TestContext.Current.CancellationToken);
        Assert.True(viewer.Offset.Y < 100);

        var target = ancestor ? Assert.IsType<Grid>(window.Content) : (Control)viewer;
        if (hidden) target.IsVisible = false;
        else target.IsEnabled = false;
        var stopped = viewer.Offset;
        await Task.Delay(80, TestContext.Current.CancellationToken);
        Assert.Equal(stopped, viewer.Offset);

        ProjectNavigationDragTests.Raise(viewer, DragDrop.DragOverEvent, transfer, new Point(60, 1));
        await Task.Delay(80, TestContext.Current.CancellationToken);
        Assert.Equal(stopped, viewer.Offset);
    });

    private Task WithViewer(Func<AppWindow, AppScrollViewer, AppCard, Task> action) => _session.DispatchAsync(async () =>
    {
        var child = new AppCard { Height = 800 };
        var viewer = new AppScrollViewer
        {
            NavigationDragScope = new object(),
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = child
        };
        var window = new AppWindow { Width = 280, Height = 200, Content = new Grid { Children = { viewer } } };
        window.Show();
        try { window.UpdateLayout(); await action(window, viewer, child); }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
