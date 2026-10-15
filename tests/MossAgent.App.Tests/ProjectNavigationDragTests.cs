using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ProjectNavigationDragTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task Drop_ShowsSharedInsertionLineAndInvokesTheMatchingRequest(bool dark, bool after) => _session.Dispatch(() =>
    {
        var scope = new object();
        var source = new object();
        AppProjectReorderRequest? executed = null;
        var row = Row(scope, new RelayCommand<AppProjectReorderRequest>(request => executed = request));
        var window = Show(row, dark);
        using var transfer = Transfer(new AppProjectDragPayload(scope, source));
        try
        {
            var position = new Point(60, after ? row.Bounds.Height - 1 : 1);
            var over = Raise(row, DragDrop.DragOverEvent, transfer, position);
            Assert.Equal(DragDropEffects.Move, over.DragEffects);
            Assert.Contains(after ? "drop-after" : "drop-before", row.Classes);
            var line = Assert.Single(row.GetVisualDescendants().OfType<AppBorder>(),
                border => border.Classes.Contains("project-drop-indicator"));
            Assert.Equal(2, line.Bounds.Height);
            Assert.Equal(1, line.Opacity);
            Assert.Equal(after ? VerticalAlignment.Bottom : VerticalAlignment.Top, line.VerticalAlignment);
            Raise(row, DragDrop.DropEvent, transfer, position);
            Assert.NotNull(executed);
            Assert.Same(scope, executed.Scope);
            Assert.Same(source, executed.Source);
            Assert.Same(row.ActionTarget, executed.Target);
            Assert.Equal(after, executed.InsertAfter);
            Assert.DoesNotContain("drop-before", row.Classes);
            Assert.DoesNotContain("drop-after", row.Classes);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("foreign")]
    [InlineData("self")]
    [InlineData("invalid")]
    [InlineData("disabled")]
    [InlineData("command")]
    [InlineData("text")]
    [InlineData("ancestor")]
    [InlineData("missing-select")]
    [InlineData("hidden")]
    [InlineData("hidden-ancestor")]
    public Task RejectedPayload_NeverPreviewsOrExecutes(string reason) => _session.Dispatch(() =>
    {
        var scope = new object();
        var count = 0;
        var row = Row(scope, new RelayCommand<AppProjectReorderRequest>(_ => count++, _ => reason != "command"));
        row.IsReorderEnabled = reason != "disabled";
        row.IsVisible = reason != "hidden";
        if (reason == "missing-select") row.SelectCommand = null;
        var payload = new AppProjectDragPayload(reason == "foreign" ? new object() : scope,
            reason == "self" ? row.ActionTarget! : new object());
        if (reason == "invalid") payload.Invalidate();
        using var transfer = reason == "text" ? new DataTransfer() : Transfer(payload);
        if (reason == "text") transfer.Add(DataTransferItem.CreateText("external project"));
        var window = Show(row, false);
        if (reason == "ancestor") Assert.IsType<StackPanel>(window.Content).IsEnabled = false;
        if (reason == "hidden-ancestor") Assert.IsType<StackPanel>(window.Content).IsVisible = false;
        try
        {
            Assert.Equal(DragDropEffects.None, Raise(row, DragDrop.DragOverEvent, transfer, new Point(60, 1)).DragEffects);
            Raise(row, DragDrop.DropEvent, transfer, new Point(60, 1));
            Assert.Equal(0, count);
            Assert.DoesNotContain("drop-before", row.Classes);
            Assert.DoesNotContain("drop-after", row.Classes);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task InvalidatedPayloadAndDetachedRow_ClearPreviewAndRestoreAllowDrop() => _session.Dispatch(() =>
    {
        var scope = new object();
        var row = Row(scope, new RelayCommand<AppProjectReorderRequest>(_ => { }));
        var window = Show(row, false);
        var payload = new AppProjectDragPayload(scope, new object());
        using var transfer = Transfer(payload);
        try
        {
            Raise(row, DragDrop.DragOverEvent, transfer, new Point(60, 1));
            Assert.Contains("drop-before", row.Classes);
            payload.Invalidate();
            Assert.DoesNotContain("drop-before", row.Classes);
            Assert.True(DragDrop.GetAllowDrop(row));
            window.Content = null;
            Assert.False(DragDrop.GetAllowDrop(row));
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task PointerThreshold_DoesNotStealClicksAndDragNeverSelectsOrOpensSummary() => _session.DispatchAsync(async () =>
    {
        var completed = new TaskCompletionSource<DragDropEffects>();
        AppProjectDragPayload? payload = null;
        var selected = 0;
        var row = new AppProjectNavigationRow((_, transfer) =>
        {
            payload = transfer.TryGetValue(AppProjectDragPayload.Format);
            return completed.Task;
        })
        {
            Title = "project",
            DragScope = new object(),
            ActionTarget = new object(),
            IsReorderEnabled = true,
            ReorderCommand = new RelayCommand<AppProjectReorderRequest>(_ => { }),
            SelectCommand = new RelayCommand(() => selected++),
            SummaryContent = new AppText { Text = "summary" }
        };
        var window = Show(row, false);
        try
        {
            var point = new Point(70, 15);
            window.MouseDown(point, MouseButton.Left);
            window.MouseMove(point + new Vector(3, 0), RawInputModifiers.LeftMouseButton);
            Assert.Null(payload);
            window.MouseUp(point, MouseButton.Left);
            Assert.Equal(1, selected);
            var select = row.FindControl<AppActionButton>("SelectProjectButton")!;
            Assert.IsType<AppFlyout>(select.Flyout).Hide();
            await Task.Delay(600, TestContext.Current.CancellationToken);
            window.MouseDown(point, MouseButton.Left);
            window.MouseMove(point + new Vector(0, 10), RawInputModifiers.LeftMouseButton);
            Assert.NotNull(payload);
            Assert.Contains("dragging", row.Classes);
            Assert.False(row.IsSummaryOpen);
            window.MouseUp(point + new Vector(0, 10), MouseButton.Left);
            Assert.Equal(1, selected);
            completed.SetResult(DragDropEffects.None);
            await Task.Yield();
            Assert.False(payload.IsValid);
            Assert.DoesNotContain("dragging", row.Classes);
        }
        finally { completed.TrySetResult(DragDropEffects.None); window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SourceLifetimeChange_InvalidatesPendingNativeDragAndIgnoresLateFailure() => _session.DispatchAsync(async () =>
    {
        var completed = new TaskCompletionSource<DragDropEffects>();
        AppProjectDragPayload? payload = null;
        var row = new AppProjectNavigationRow((_, transfer) =>
        {
            payload = transfer.TryGetValue(AppProjectDragPayload.Format);
            return completed.Task;
        })
        {
            Title = "old project",
            DragScope = new object(),
            ActionTarget = new object(),
            IsReorderEnabled = true,
            ReorderCommand = new RelayCommand<AppProjectReorderRequest>(_ => { }),
            SelectCommand = new RelayCommand(() => { })
        };
        var window = Show(row, false);
        try
        {
            window.MouseDown(new Point(70, 15), MouseButton.Left);
            window.MouseMove(new Point(70, 25), RawInputModifiers.LeftMouseButton);
            Assert.NotNull(payload);
            row.ActionTarget = new object();
            row.Title = "new project";
            Assert.False(payload.IsValid);
            Assert.DoesNotContain("dragging", row.Classes);
            completed.SetException(new IOException("late secret sentinel"));
            await Task.Yield();
            Assert.Equal(row.Title, row.DragHint);
        }
        finally { completed.TrySetResult(DragDropEffects.None); window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task NativeFailure_UpdatesLocalizedTooltipAndDetachmentClearsIt() => _session.Dispatch(() =>
    {
        var row = new AppProjectNavigationRow((_, _) => Task.FromException<DragDropEffects>(new IOException("secret sentinel")))
        {
            Title = "project",
            DragScope = new object(),
            ActionTarget = new object(),
            IsReorderEnabled = true,
            ReorderCommand = new RelayCommand<AppProjectReorderRequest>(_ => { }),
            SelectCommand = new RelayCommand(() => { })
        };
        var window = Show(row, false);
        try
        {
            window.MouseDown(new Point(70, 15), MouseButton.Left);
            window.MouseMove(new Point(70, 25), RawInputModifiers.LeftMouseButton);
            Assert.NotEqual(row.Title, row.DragHint);
            Assert.DoesNotContain("secret sentinel", row.DragHint);
            Assert.Equal(row.DragHint, ToolTip.GetTip(row.FindControl<AppActionButton>("SelectProjectButton")!));
            window.MouseUp(new Point(70, 25), MouseButton.Left);
            window.Content = null;
            Assert.Equal(row.Title, row.DragHint);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static AppProjectNavigationRow Row(object scope, RelayCommand<AppProjectReorderRequest> command) => new()
    {
        Title = "project",
        DragScope = scope,
        ActionTarget = new object(),
        IsReorderEnabled = true,
        ReorderCommand = command,
        SelectCommand = new RelayCommand(() => { })
    };

    private static AppWindow Show(Control content, bool dark)
    {
        var window = new AppWindow
        {
            Width = 280,
            Height = 120,
            Content = new StackPanel { Children = { content } },
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    internal static DataTransfer Transfer(AppProjectDragPayload payload)
    {
        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.Create(AppProjectDragPayload.Format, payload));
        return transfer;
    }

    internal static DragEventArgs Raise(Interactive target, RoutedEvent<DragEventArgs> routedEvent,
        IDataTransfer transfer, Point position)
    {
        var args = new DragEventArgs(routedEvent, transfer, target, position, KeyModifiers.None)
        {
            Source = target,
            DragEffects = DragDropEffects.Move
        };
        target.RaiseEvent(args);
        return args;
    }
}
