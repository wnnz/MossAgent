using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace MossAgent.App.Controls;

internal sealed class AppNavigationDragScroller : IDisposable
{
    private readonly AppScrollViewer _viewer;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly bool _previousAllowDrop;
    private AppProjectDragPayload? _payload;
    private Point _position;
    private bool _disposed;
    private bool CanScroll(AppProjectDragPayload? payload) => !_disposed
        && _viewer.IsEffectivelyVisible && _viewer.IsEffectivelyEnabled
        && payload is { IsValid: true } && ReferenceEquals(payload.Scope, _viewer.NavigationDragScope);

    public AppNavigationDragScroller(AppScrollViewer viewer)
    {
        _viewer = viewer;
        _previousAllowDrop = DragDrop.GetAllowDrop(viewer);
        DragDrop.SetAllowDrop(viewer, true);
        viewer.AddHandler(DragDrop.DragOverEvent, HandleOver, RoutingStrategies.Bubble, true);
        viewer.AddHandler(DragDrop.DragLeaveEvent, HandleLeave, RoutingStrategies.Bubble, true);
        viewer.AddHandler(DragDrop.DropEvent, HandleDrop, RoutingStrategies.Bubble, true);
        _timer.Tick += HandleTick;
    }

    private void HandleOver(object? sender, DragEventArgs args)
    {
        AppProjectDragPayload? payload;
        try { payload = args.DataTransfer.TryGetValue(AppProjectDragPayload.Format); }
        catch (Exception) { Stop(); return; }
        if (!CanScroll(payload))
        {
            Stop();
            return;
        }
        if (!ReferenceEquals(payload, _payload))
        {
            Stop();
            _payload = payload;
            payload!.Invalidated += Stop;
        }
        _position = args.GetPosition(_viewer);
        if (Delta() == 0) _timer.Stop();
        else if (!_timer.IsEnabled)
        {
            var previous = _viewer.Offset;
            HandleTick(this, EventArgs.Empty);
            if (_viewer.Offset != previous) _timer.Start();
        }
    }

    private double Delta()
    {
        var height = _viewer.Viewport.Height;
        if (_position.X < 0 || _position.X > _viewer.Viewport.Width || _position.Y < 0 || _position.Y > height) return 0;
        const double edge = 32;
        if (_position.Y < edge) return -8 * (edge - _position.Y) / edge;
        if (_position.Y > height - edge) return 8 * (_position.Y - height + edge) / edge;
        return 0;
    }

    private void HandleTick(object? sender, EventArgs args)
    {
        if (!CanScroll(_payload))
        {
            Stop();
            return;
        }
        var maximum = Math.Max(0, _viewer.Extent.Height - _viewer.Viewport.Height);
        var offset = Math.Clamp(_viewer.Offset.Y + Delta(), 0, maximum);
        if (Math.Abs(offset - _viewer.Offset.Y) < 0.01) { _timer.Stop(); return; }
        _viewer.Offset = new Vector(_viewer.Offset.X, offset);
    }

    private void HandleLeave(object? sender, DragEventArgs args)
    {
        var position = args.GetPosition(_viewer);
        if (ReferenceEquals(args.Source, _viewer) || !new Rect(_viewer.Bounds.Size).Contains(position)) Stop();
    }

    private void HandleDrop(object? sender, DragEventArgs args) => Stop();
    private void Stop()
    {
        _timer.Stop();
        if (_payload is not null) _payload.Invalidated -= Stop;
        _payload = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _timer.Tick -= HandleTick;
        _viewer.RemoveHandler(DragDrop.DragOverEvent, HandleOver);
        _viewer.RemoveHandler(DragDrop.DragLeaveEvent, HandleLeave);
        _viewer.RemoveHandler(DragDrop.DropEvent, HandleDrop);
        DragDrop.SetAllowDrop(_viewer, _previousAllowDrop);
    }
}
