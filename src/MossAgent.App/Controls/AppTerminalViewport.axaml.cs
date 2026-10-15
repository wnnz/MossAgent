using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Windows.Input;
using MossAgent.Tools.Abstractions.Terminal;
using MossAgent.App.Terminal;

namespace MossAgent.App.Controls;

public sealed partial class AppTerminalViewport : AppUserControl
{
    public static readonly StyledProperty<TerminalFrame?> FrameProperty =
        AvaloniaProperty.Register<AppTerminalViewport, TerminalFrame?>(nameof(Frame));
    public static readonly StyledProperty<ICommand?> ResizeCommandProperty =
        AvaloniaProperty.Register<AppTerminalViewport, ICommand?>(nameof(ResizeCommand));
    public static readonly StyledProperty<ICommand?> InputCommandProperty =
        AvaloniaProperty.Register<AppTerminalViewport, ICommand?>(nameof(InputCommand));
    public static readonly StyledProperty<int> InputScopeVersionProperty =
        AvaloniaProperty.Register<AppTerminalViewport, int>(nameof(InputScopeVersion));
    private readonly Dictionary<(Guid View, bool Alternate), TerminalScrollPosition> _positions = [];
    private (Guid View, bool Alternate)? _key;
    private long _visits;
    private bool _restoring;
    private Vector _requested;
    private bool _follow = true;
    private bool _subscribed;
    private ICommand? _resizeSource;
    private readonly TerminalViewportInputMethod? _inputMethod;
    private readonly List<Visual> _ancestors = [];
    private AppScrollViewer Scroller { get; }
    private AppTerminalScreen Screen { get; }

    public AppTerminalViewport()
    {
        AvaloniaXamlLoader.Load(this);
        Scroller = this.FindControl<AppScrollViewer>("TerminalScroller")!;
        Screen = this.FindControl<AppTerminalScreen>("TerminalGrid")!;
        _inputMethod = new(this, Screen, this.FindControl<AppText>("TerminalPreedit")!, Scroller);
        ClipboardActions = new(Screen.Selection, () => _subscribed && IsEffectivelyVisible && IsEffectivelyEnabled
            && TopLevel.GetTopLevel(this)?.Clipboard is not null,
            text => TopLevel.GetTopLevel(this)!.Clipboard!.SetTextAsync(text),
            () => TopLevel.GetTopLevel(this)!.Clipboard!.TryGetTextAsync());
        Classes.Add("app-terminal-viewport");
        AddHandler(KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, HandleTextInput, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, HandlePointerFocus, RoutingStrategies.Tunnel);
    }
    public TerminalClipboardCommands ClipboardActions { get; }
    public TerminalFrame? Frame { get => GetValue(FrameProperty); set => SetValue(FrameProperty, value); }
    public ICommand? ResizeCommand { get => GetValue(ResizeCommandProperty); set => SetValue(ResizeCommandProperty, value); }
    public ICommand? InputCommand { get => GetValue(InputCommandProperty); set => SetValue(InputCommandProperty, value); }
    public int InputScopeVersion { get => GetValue(InputScopeVersionProperty); set => SetValue(InputScopeVersionProperty, value); }

    private void HandleKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Handled || !_subscribed || !IsEffectivelyVisible || !IsEffectivelyEnabled || Frame is not { SessionId: { } session } frame) return;
        if (_inputMethod?.IsComposing == true) return;
        if (eventArgs.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && eventArgs.Key is Key.C or Key.V or Key.A)
        {
            var command = eventArgs.Key == Key.C ? (ICommand)ClipboardActions.CopyCommand
                : eventArgs.Key == Key.V ? ClipboardActions.PasteCommand : ClipboardActions.SelectAllCommand;
            if (command.CanExecute(null)) command.Execute(null);
            eventArgs.Handled = true;
            return;
        }
        if (eventArgs.Key == Key.Escape && Screen.Selection.HasSelection)
        { Screen.Selection.Clear(); eventArgs.Handled = true; return; }
        var request = new TerminalInputRequest(frame.ViewId, session, Key: eventArgs.Key, Modifiers: eventArgs.KeyModifiers, ScopeGeneration: InputScopeVersion);
        if (TerminalKeyEncoder.Encode(request, frame) is not null && InputCommand?.CanExecute(request) == true)
        {
            InputCommand.Execute(request);
            eventArgs.Handled = true;
        }
    }

    private void HandleTextInput(object? sender, TextInputEventArgs eventArgs)
    {
        if (eventArgs.Handled || !_subscribed || !IsEffectivelyVisible || !IsEffectivelyEnabled || string.IsNullOrEmpty(eventArgs.Text)
            || Frame is not { SessionId: { } session } frame) return;
        var request = new TerminalInputRequest(frame.ViewId, session, Text: eventArgs.Text, ScopeGeneration: InputScopeVersion);
        if (InputCommand?.CanExecute(request) == true) { _inputMethod?.Complete(); InputCommand.Execute(request); eventArgs.Handled = true; }
    }

    private void HandlePointerFocus(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (!eventArgs.Handled && eventArgs.Source is AppTerminalScreen && eventArgs.GetCurrentPoint(this).Properties.IsLeftButtonPressed) Focus();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (ClipboardActions is not null && (change.Property == InputCommandProperty || change.Property == InputScopeVersionProperty))
            ClipboardActions.Observe(Frame, _subscribed ? InputCommand : null, InputScopeVersion);
        if (change.Property == InputCommandProperty || change.Property == InputScopeVersionProperty) _inputMethod?.Observe(true);
        if (ClipboardActions is not null && (change.Property == IsVisibleProperty || change.Property == IsEnabledProperty)) RefreshAvailability();
        if (change.Property == ResizeCommandProperty) { BindResizeCommand(); InvalidateArrange(); return; }
        if (change.Property != FrameProperty) return;
        var next = Frame is { } frame ? (frame.ViewId, frame.AlternateScreen) : ((Guid, bool)?)null;
        if (next != _key)
        {
            SavePosition();
            _key = next;
            var position = next is { } key ? _positions.GetValueOrDefault(key) : null;
            _requested = position?.Offset ?? default;
            _follow = position?.Follow ?? Frame?.AlternateScreen != true;
        }
        else _requested = Scroller.Offset;
        _restoring = true;
        Screen.Frame = Frame;
        _inputMethod?.Observe();
        ClipboardActions?.Observe(Frame, _subscribed ? InputCommand : null, InputScopeVersion);
        InvalidateArrange();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);
        if (_restoring) RestorePosition();
        RequestSize();
        _inputMethod?.RefreshGeometry();
        return result;
    }

    private void RequestSize()
    {
        if (!_subscribed || !IsEffectivelyVisible || !IsEffectivelyEnabled || Frame?.SessionId is null || Screen.CellWidth <= 0
            || Scroller.Viewport.Width <= 0 || Scroller.Viewport.Height <= 0) return;
        var size = new TerminalGridSize(Math.Clamp((int)Math.Floor(Scroller.Viewport.Width / Screen.CellWidth), 20, 500),
            Math.Clamp((int)Math.Floor(Scroller.Viewport.Height / Screen.CellHeight), 5, 200));
        if (ResizeCommand?.CanExecute(size) == true) ResizeCommand.Execute(size);
    }

    private void BindResizeCommand()
    {
        if (_resizeSource is not null) _resizeSource.CanExecuteChanged -= OnResizeEligibilityChanged;
        _resizeSource = _subscribed ? ResizeCommand : null;
        if (_resizeSource is not null) _resizeSource.CanExecuteChanged += OnResizeEligibilityChanged;
    }

    private void OnResizeEligibilityChanged(object? sender, EventArgs eventArgs) => InvalidateArrange();

    private void RestorePosition()
    {
        var end = Math.Max(0, Scroller.Extent.Height - Scroller.Viewport.Height);
        Scroller.Offset = new(_requested.X, _follow ? end : _requested.Y);
        Screen.SetViewport(Scroller.Offset, Scroller.Viewport);
        _restoring = false;
    }

    private void SavePosition()
    {
        if (_key is not { } key) return;
        if (!_positions.ContainsKey(key) && _positions.Count == 16)
            _positions.Remove(_positions.MinBy(pair => pair.Value.Visit).Key);
        _positions[key] = new(Scroller.Offset, _follow, ++_visits);
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs eventArgs)
    {
        if (_restoring) return;
        if (eventArgs.ExtentDelta != default || eventArgs.ViewportDelta != default)
        {
            _requested = Scroller.Offset;
            _restoring = true;
            RestorePosition();
        }
        else
        {
            Screen.SetViewport(Scroller.Offset, Scroller.Viewport);
            _follow = Scroller.Offset.Y >= Math.Max(0, Scroller.Extent.Height - Scroller.Viewport.Height) - 1;
            SavePosition();
        }
        _inputMethod?.RefreshGeometry();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnAttachedToVisualTree(eventArgs);
        if (_subscribed) return;
        _subscribed = true;
        _inputMethod?.Attach();
        foreach (var ancestor in this.GetVisualAncestors())
        {
            _ancestors.Add(ancestor);
            ancestor.PropertyChanged += HandleAncestorChanged;
        }
        Screen.Selection.Changed += HandleSelectionChanged;
        ClipboardActions.Observe(Frame, InputCommand, InputScopeVersion);
        Scroller.ScrollChanged += OnScrollChanged;
        BindResizeCommand();
        _restoring = true;
        InvalidateArrange();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        SavePosition();
        if (_subscribed) Scroller.ScrollChanged -= OnScrollChanged;
        _subscribed = false;
        _inputMethod?.Detach();
        foreach (var ancestor in _ancestors) ancestor.PropertyChanged -= HandleAncestorChanged;
        _ancestors.Clear();
        Screen.Selection.Changed -= HandleSelectionChanged;
        Screen.ResetSelection();
        ClipboardActions.Observe(null, null, InputScopeVersion);
        BindResizeCommand();
        base.OnDetachedFromVisualTree(eventArgs);
    }

    private void HandleSelectionChanged(object? sender, EventArgs eventArgs) => ClipboardActions.RefreshSelection();
    private void HandleAncestorChanged(object? sender, AvaloniaPropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.Property == IsVisibleProperty || eventArgs.Property == IsEnabledProperty) RefreshAvailability();
    }
    private void RefreshAvailability()
    {
        ClipboardActions.RefreshAvailability();
        _inputMethod?.Observe();
        if (!IsEffectivelyVisible || !IsEffectivelyEnabled) Screen.ResetSelection();
    }
}
