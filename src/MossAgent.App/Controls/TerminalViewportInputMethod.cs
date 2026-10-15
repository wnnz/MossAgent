using System.Windows.Input;
using Avalonia;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;
using Avalonia.Media;
using MossAgent.App.Terminal;

namespace MossAgent.App.Controls;

internal sealed class TerminalViewportInputMethod(AppTerminalViewport viewport, AppTerminalScreen screen,
    AppText preedit, AppScrollViewer scroller)
{
    private TerminalInputMethodClient? _client;
    private ICommand? _command;
    private readonly TranslateTransform _position = new();
    private bool _attached;
    private bool _wasAvailable;
    public bool IsComposing => _client?.IsComposing == true;
    private bool Available => _attached && viewport.IsKeyboardFocusWithin && viewport.IsEffectivelyVisible
        && viewport.IsEffectivelyEnabled && viewport.Frame is { SessionId: { } session, Failed: false } frame
        && viewport.InputCommand?.CanExecute(new TerminalInputRequest(frame.ViewId, session, Text: string.Empty,
            ScopeGeneration: viewport.InputScopeVersion)) == true;

    public void Attach()
    {
        if (_attached) return;
        _attached = true;
        viewport.TextInputMethodClientRequested += HandleClientRequested;
        viewport.LostFocus += HandleLostFocus;
        Observe();
    }

    public void Detach()
    {
        _attached = false;
        _wasAvailable = false;
        viewport.TextInputMethodClientRequested -= HandleClientRequested;
        viewport.LostFocus -= HandleLostFocus;
        if (_command is not null) _command.CanExecuteChanged -= HandleEligibilityChanged;
        _command = null;
        Invalidate();
    }

    public void Observe(bool changed = false)
    {
        if (!ReferenceEquals(_command, viewport.InputCommand))
        {
            if (_command is not null) _command.CanExecuteChanged -= HandleEligibilityChanged;
            _command = _attached ? viewport.InputCommand : null;
            if (_command is not null) _command.CanExecuteChanged += HandleEligibilityChanged;
            changed = true;
        }
        var available = Available;
        var becameAvailable = available && !_wasAvailable;
        _wasAvailable = available;
        if (changed || !available || _client is { IsAvailable: false }) Invalidate();
        if (_attached && available && _client is null && (changed || becameAvailable)) Requery();
        RefreshGeometry();
    }

    public void Complete() => _client?.Clear(false);

    private void HandleClientRequested(object? sender, TextInputMethodClientRequestedEventArgs eventArgs)
    {
        if (eventArgs.Client is not null || !Available) return;
        if (_client is null)
        {
            var frame = viewport.Frame!;
            var generation = viewport.InputScopeVersion;
            var command = viewport.InputCommand;
            _client = new(viewport, () => Available && viewport.Frame is { } current
                && current.ViewId == frame.ViewId && current.TaskId == frame.TaskId && current.SessionId == frame.SessionId
                && current.AlternateScreen == frame.AlternateScreen && viewport.InputScopeVersion == generation
                && ReferenceEquals(viewport.InputCommand, command), CursorRectangle, Present);
        }
        eventArgs.Client = _client;
    }

    private void Present(string text)
    {
        preedit.Text = text;
        preedit.IsVisible = text.Length > 0;
        RefreshGeometry();
    }

    private Rect VisibleArea()
    {
        var origin = scroller.TranslatePoint(default, viewport) ?? default;
        return new(origin, new Size(Math.Min(scroller.Viewport.Width, Math.Max(0, viewport.Bounds.Width - origin.X)),
            Math.Min(scroller.Viewport.Height, Math.Max(0, viewport.Bounds.Height - origin.Y))));
    }

    private Rect Anchor(Rect area)
    {
        if (viewport.Frame is not { } frame || screen.CellWidth <= 0) return default;
        var cellWidth = Math.Min(screen.CellWidth, area.Width);
        var cellHeight = Math.Min(screen.CellHeight, area.Height);
        var cursor = screen.TranslatePoint(new(frame.CursorColumn * screen.CellWidth, frame.CursorRow * screen.CellHeight), viewport) ?? area.Position;
        return new(Math.Clamp(cursor.X, area.Left, area.Right - cellWidth),
            Math.Clamp(cursor.Y, area.Top, area.Bottom - cellHeight), cellWidth, cellHeight);
    }

    private Rect CursorRectangle()
    {
        var area = VisibleArea();
        var anchor = Anchor(area);
        if (!preedit.IsVisible || _client is null) return anchor;
        var caret = preedit.TextLayout.HitTestTextPosition(_client.Cursor);
        var right = area.Right - anchor.Width;
        return anchor.WithX(Math.Clamp(anchor.X + caret.X, anchor.X, Math.Max(anchor.X, right)));
    }

    public void RefreshGeometry()
    {
        var area = VisibleArea();
        var anchor = Anchor(area);
        preedit.RenderTransform = _position;
        _position.X = anchor.X;
        _position.Y = anchor.Y;
        preedit.MaxWidth = Math.Max(0, area.Right - anchor.X);
        _client?.RefreshCursor();
    }

    private void Invalidate()
    {
        var previous = _client;
        _client = null;
        previous?.Invalidate();
        preedit.IsVisible = false;
        if (_attached && previous is not null)
            Requery();
    }

    private void Requery() => viewport.RaiseEvent(new TextInputMethodClientRequeryRequestedEventArgs
    { RoutedEvent = InputMethod.TextInputMethodClientRequeryRequestedEvent });

    private void HandleEligibilityChanged(object? sender, EventArgs eventArgs) => Observe();
    private void HandleLostFocus(object? sender, RoutedEventArgs eventArgs) => Invalidate();
}
