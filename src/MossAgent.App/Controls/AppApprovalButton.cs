using System.Windows.Input;
using Avalonia;
using Avalonia.Input;

namespace MossAgent.App.Controls;

public sealed class AppApprovalButton : AppButton
{
    private readonly HashSet<Key> _heldKeys = [];
    private ICommand? _pressedCommand;
    private object? _pressedParameter;
    private bool _hasGesture;
    private bool _pointerDown;
    private bool _releasingPointer;

    protected override Type StyleKeyOverride => typeof(AppButton);

    protected override void OnKeyDown(KeyEventArgs eventArgs)
    {
        if (eventArgs.Key is Key.Space or Key.Enter)
        {
            BeginGesture();
            _heldKeys.Add(eventArgs.Key);
        }
        base.OnKeyDown(eventArgs);
    }

    protected override void OnKeyUp(KeyEventArgs eventArgs)
    {
        try { base.OnKeyUp(eventArgs); }
        finally
        {
            _heldKeys.Remove(eventArgs.Key);
            EndReleasedGesture();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs eventArgs)
    {
        if (eventArgs.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginGesture();
            _pointerDown = true;
        }
        base.OnPointerPressed(eventArgs);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs eventArgs)
    {
        _releasingPointer = true;
        try { base.OnPointerReleased(eventArgs); }
        finally
        {
            _releasingPointer = false;
            if (eventArgs.InitialPressMouseButton == MouseButton.Left) _pointerDown = false;
            EndReleasedGesture();
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs eventArgs)
    {
        base.OnPointerCaptureLost(eventArgs);
        _pointerDown = false;
        if (!_releasingPointer) EndReleasedGesture();
    }

    protected override void OnClick()
    {
        if (VisualRoot is null || !IsEffectivelyVisible || !IsEffectivelyEnabled
            || CommandParameter is not Guid
            || (_hasGesture && (!ReferenceEquals(_pressedCommand, Command)
                || !Equals(_pressedParameter, CommandParameter)))) return;
        base.OnClick();
    }

    protected override void OnLostFocus(FocusChangedEventArgs eventArgs)
    {
        _heldKeys.Clear();
        EndReleasedGesture();
        base.OnLostFocus(eventArgs);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        _heldKeys.Clear();
        _pointerDown = false;
        EndReleasedGesture();
        base.OnDetachedFromVisualTree(eventArgs);
    }

    private void BeginGesture()
    {
        if (_heldKeys.Count > 0 || _pointerDown) return;
        _pressedCommand = Command;
        _pressedParameter = CommandParameter;
        _hasGesture = true;
    }

    private void EndReleasedGesture()
    {
        if (_heldKeys.Count > 0 || _pointerDown || _releasingPointer) return;
        _hasGesture = false;
        _pressedCommand = null;
        _pressedParameter = null;
    }
}
