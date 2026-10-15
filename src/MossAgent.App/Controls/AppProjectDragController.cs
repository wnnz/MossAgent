using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace MossAgent.App.Controls;

internal sealed class AppProjectDragController : IDisposable
{
    private readonly AppProjectNavigationRow _row;
    private readonly AppActionButton _button;
    private readonly Func<PointerPressedEventArgs, IDataTransfer, Task<DragDropEffects>> _startDrag;
    private readonly bool _previousAllowDrop;
    private PointerPressedEventArgs? _pressed;
    private Point _origin;
    private AppProjectDragPayload? _active;
    private AppProjectDragPayload? _preview;
    private ICommand? _command;
    private bool _dragging;
    private bool _disposed;

    public AppProjectDragController(AppProjectNavigationRow row, AppActionButton button,
        Func<PointerPressedEventArgs, IDataTransfer, Task<DragDropEffects>>? startDrag = null)
    {
        _row = row;
        _button = button;
        _startDrag = startDrag ?? ((args, data) => DragDrop.DoDragDropAsync(args, data, DragDropEffects.Move));
        _previousAllowDrop = DragDrop.GetAllowDrop(row);
        DragDrop.SetAllowDrop(row, true);
        row.AddHandler(InputElement.PointerPressedEvent, HandlePressed, RoutingStrategies.Tunnel, true);
        row.AddHandler(InputElement.PointerMovedEvent, HandleMoved, RoutingStrategies.Tunnel, true);
        row.AddHandler(InputElement.PointerReleasedEvent, HandleReleased, RoutingStrategies.Tunnel, true);
        row.AddHandler(DragDrop.DragOverEvent, HandleOver);
        row.AddHandler(DragDrop.DragLeaveEvent, HandleLeave);
        row.AddHandler(DragDrop.DropEvent, HandleDrop);
        SynchronizeCommand();
    }

    public void Reset()
    {
        var pressed = _pressed;
        _pressed = null;
        if (pressed?.Pointer.Captured is Visual captured && IsButtonSurface(captured))
            pressed.Pointer.Capture(null);
        _active?.Invalidate();
        ClearPreview();
        _row.Classes.Remove("dragging");
    }

    public void SynchronizeCommand()
    {
        if (_command is not null) _command.CanExecuteChanged -= HandleCommandChanged;
        _command = _row.ReorderCommand;
        if (_command is not null) _command.CanExecuteChanged += HandleCommandChanged;
        Reset();
    }

    private bool IsButtonSurface(object? source) => source is Visual visual
        && (ReferenceEquals(visual, _button) || visual.GetVisualAncestors().Contains(_button));

    private bool CanStart()
    {
        try
        {
            return !_disposed && _row.IsReorderEnabled && _row.IsEffectivelyEnabled && _button.IsEffectivelyEnabled
                && _row.IsEffectivelyVisible && _button.IsEffectivelyVisible
                && _row.DragScope is { } scope && _row.ActionTarget is { } target
                && _row.ReorderCommand?.CanExecute(new AppProjectReorderRequest(scope, target, target, false)) == true;
        }
        catch (Exception) { return false; }
    }

    private void HandlePressed(object? sender, PointerPressedEventArgs args)
    {
        if (_dragging || !CanStart() || !IsButtonSurface(args.Source)
            || args.Pointer.Type != PointerType.Mouse || args.ClickCount != 1
            || !args.GetCurrentPoint(_row).Properties.IsLeftButtonPressed) return;
        _pressed = args;
        _origin = args.GetPosition(_row);
        _row.SetDragFailed(false);
    }

    private void HandleMoved(object? sender, PointerEventArgs args)
    {
        if (_pressed is not { } pressed || _dragging || !CanStart()) return;
        if (!args.GetCurrentPoint(_row).Properties.IsLeftButtonPressed)
        {
            _pressed = null;
            return;
        }
        var distance = args.GetPosition(_row) - _origin;
        if (Math.Abs(distance.X) < 6 && Math.Abs(distance.Y) < 6) return;
        _pressed = null;
        args.Handled = true;
        pressed.Pointer.Capture(null);
        _ = StartAsync(pressed);
    }

    private async Task StartAsync(PointerPressedEventArgs pressed)
    {
        if (!CanStart()) return;
        _dragging = true;
        var payload = new AppProjectDragPayload(_row.DragScope!, _row.ActionTarget!);
        _active = payload;
        _row.IsSummaryOpen = false;
        _row.Classes.Add("dragging");
        using var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.Create(AppProjectDragPayload.Format, payload));
        try { await _startDrag(pressed, transfer); }
        catch (Exception) { if (!_disposed && payload.IsValid) _row.SetDragFailed(true); }
        finally
        {
            payload.Invalidate();
            _active = null;
            _dragging = false;
            if (!_disposed) _row.Classes.Remove("dragging");
        }
    }

    private AppProjectReorderRequest? Request(DragEventArgs args, out AppProjectDragPayload? payload)
    {
        payload = null;
        try
        {
            payload = args.DataTransfer.TryGetValue(AppProjectDragPayload.Format);
            if (!CanStart() || payload is not { IsValid: true } || !ReferenceEquals(payload.Scope, _row.DragScope)
                || ReferenceEquals(payload.Source, _row.ActionTarget)) return null;
            var request = new AppProjectReorderRequest(payload.Scope, payload.Source, _row.ActionTarget!,
                args.GetPosition(_row).Y >= _row.Bounds.Height / 2);
            return _row.ReorderCommand!.CanExecute(request) ? request : null;
        }
        catch (Exception) { return null; }
    }

    private void HandleOver(object? sender, DragEventArgs args)
    {
        var request = Request(args, out var payload);
        args.DragEffects = request is null ? DragDropEffects.None : DragDropEffects.Move;
        args.Handled = payload is not null;
        if (request is null) { ClearPreview(); return; }
        if (!ReferenceEquals(payload, _preview))
        {
            ClearPreview();
            _preview = payload;
            _preview!.Invalidated += ClearPreview;
        }
        _row.Classes.Set("drop-before", !request.InsertAfter);
        _row.Classes.Set("drop-after", request.InsertAfter);
    }

    private void HandleDrop(object? sender, DragEventArgs args)
    {
        var request = Request(args, out var payload);
        ClearPreview();
        args.DragEffects = request is null ? DragDropEffects.None : DragDropEffects.Move;
        args.Handled = payload is not null;
        if (request is null) return;
        _row.IsSummaryOpen = false;
        _row.ReorderCommand!.Execute(request);
    }

    private void HandleLeave(object? sender, DragEventArgs args) => ClearPreview();
    private void HandleReleased(object? sender, PointerReleasedEventArgs args) => _pressed = null;
    private void HandleCommandChanged(object? sender, EventArgs args)
    {
        if (!CanStart()) Reset();
        else ClearPreview();
    }

    private void ClearPreview()
    {
        if (_preview is not null) _preview.Invalidated -= ClearPreview;
        _preview = null;
        _row.Classes.Remove("drop-before");
        _row.Classes.Remove("drop-after");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Reset();
        if (_command is not null) _command.CanExecuteChanged -= HandleCommandChanged;
        _row.RemoveHandler(InputElement.PointerPressedEvent, HandlePressed);
        _row.RemoveHandler(InputElement.PointerMovedEvent, HandleMoved);
        _row.RemoveHandler(InputElement.PointerReleasedEvent, HandleReleased);
        _row.RemoveHandler(DragDrop.DragOverEvent, HandleOver);
        _row.RemoveHandler(DragDrop.DragLeaveEvent, HandleLeave);
        _row.RemoveHandler(DragDrop.DropEvent, HandleDrop);
        DragDrop.SetAllowDrop(_row, _previousAllowDrop);
    }
}
