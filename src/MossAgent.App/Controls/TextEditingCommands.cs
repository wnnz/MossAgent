using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;

namespace MossAgent.App.Controls;

internal sealed class TextEditingCommands : IDisposable
{
    private readonly Window _window;
    private readonly Func<Control, bool> _isMenuFocus;
    private readonly Dictionary<TextEditingAction, RelayCommand> _commands;
    private readonly List<Control> _watchedControls = [];
    private Control? _target;
    private string? _selectionText;
    private int _selectionStart;
    private int _selectionEnd;
    private bool _disposed;

    public TextEditingCommands(Window window, Func<Control, bool> isMenuFocus)
    {
        _window = window;
        _isMenuFocus = isMenuFocus;
        _commands = Enum.GetValues<TextEditingAction>().ToDictionary(action => action,
            action => new RelayCommand(() => Execute(action), () => CanExecute(action)));
        _window.GotFocus += HandleFocusChanged;
        _window.Closed += HandleWindowClosed;
        CaptureFocus(_window.FocusManager?.GetFocusedElement() as Control);
    }

    public IRelayCommand this[TextEditingAction action] => _commands[action];

    public void Refresh()
    {
        foreach (var command in _commands.Values) command.NotifyCanExecuteChanged();
    }

    public void PrepareForMenu()
    {
        RestoreSelection();
        Refresh();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _window.GotFocus -= HandleFocusChanged;
        _window.Closed -= HandleWindowClosed;
        SetTarget(null);
    }

    private bool CanExecute(TextEditingAction action)
    {
        if (!IsAvailableTarget()) return false;

        if (_target is SelectableTextBlock text)
            return action == TextEditingAction.Copy && text.CanCopy
                || action == TextEditingAction.SelectAll && !string.IsNullOrEmpty(text.Text);
        if (_target is not TextBox input) return false;
        return action switch
        {
            TextEditingAction.Undo => !input.IsReadOnly && input.CanUndo,
            TextEditingAction.Redo => !input.IsReadOnly && input.CanRedo,
            TextEditingAction.Cut => !input.IsReadOnly && input.PasswordChar == '\0' && input.CanCut,
            TextEditingAction.Copy => input.PasswordChar == '\0' && input.CanCopy,
            TextEditingAction.Paste => !input.IsReadOnly && input.CanPaste,
            TextEditingAction.SelectAll => !string.IsNullOrEmpty(input.Text),
            _ => false
        };
    }

    private bool IsAvailableTarget() => !_disposed && _target is not null
        && ReferenceEquals(TopLevel.GetTopLevel(_target), _window)
        && _target.IsEffectivelyVisible && _target.IsEffectivelyEnabled
        && _target.GetVisualAncestors().OfType<Control>().All(control => control.IsVisible && control.IsEnabled);

    private void Execute(TextEditingAction action)
    {
        if (!CanExecute(action)) return;
        _target!.Focus();
        if (_target is TextBox input)
        {
            switch (action)
            {
                case TextEditingAction.Undo: input.Undo(); break;
                case TextEditingAction.Redo: input.Redo(); break;
                case TextEditingAction.Cut: input.Cut(); break;
                case TextEditingAction.Copy: input.Copy(); break;
                case TextEditingAction.Paste: input.Paste(); break;
                case TextEditingAction.SelectAll: input.SelectAll(); break;
            }
        }
        else if (_target is SelectableTextBlock text)
        {
            if (action == TextEditingAction.Copy) text.Copy();
            if (action == TextEditingAction.SelectAll) text.SelectAll();
        }
        Refresh();
    }

    private void HandleFocusChanged(object? sender, FocusChangedEventArgs args) =>
        CaptureFocus(args.NewFocusedElement as Control);

    private void CaptureFocus(Control? focused)
    {
        if (_disposed || focused is null) return;
        if (_isMenuFocus(focused))
        {
            PrepareForMenu();
            return;
        }
        var editor = focused.GetVisualAncestors().Prepend(focused)
            .OfType<Control>().FirstOrDefault(control => control is TextBox or SelectableTextBlock);
        SetTarget(editor);
    }

    private void SetTarget(Control? target)
    {
        if (ReferenceEquals(_target, target))
        {
            if (target?.IsKeyboardFocusWithin == true) RememberSelection();
            Refresh();
            return;
        }
        foreach (var control in _watchedControls) control.PropertyChanged -= HandleTargetPropertyChanged;
        _watchedControls.Clear();
        if (_target is not null) _target.DetachedFromVisualTree -= HandleTargetDetached;
        _target = target;
        RememberSelection();
        if (target is not null)
        {
            _watchedControls.AddRange(target.GetVisualAncestors().Prepend(target).OfType<Control>());
            foreach (var control in _watchedControls) control.PropertyChanged += HandleTargetPropertyChanged;
            target.DetachedFromVisualTree += HandleTargetDetached;
        }
        Refresh();
    }

    private void HandleTargetPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (ReferenceEquals(sender, _target))
        {
            if (args.Property.Name == "Text" && !_target!.IsKeyboardFocusWithin) _selectionText = null;
            else if (args.Property.Name is "Text" or "SelectionStart" or "SelectionEnd"
                && _target!.IsKeyboardFocusWithin) RememberSelection();
        }
        if (args.Property.Name is "Text" or "SelectionStart" or "SelectionEnd" or "IsReadOnly"
            or "PasswordChar" or "CanUndo" or "CanRedo" or "CanCopy" or "CanCut" or "CanPaste"
            or "IsVisible" or "IsEnabled") Refresh();
    }

    private void RememberSelection()
    {
        (_selectionText, _selectionStart, _selectionEnd) = _target switch
        {
            TextBox input => (input.Text, input.SelectionStart, input.SelectionEnd),
            SelectableTextBlock text => (text.Text, text.SelectionStart, text.SelectionEnd),
            _ => (null, 0, 0)
        };
    }

    private void RestoreSelection()
    {
        if (!IsAvailableTarget() || _selectionText is null) return;
        if (_target is TextBox input && input.Text == _selectionText)
        {
            input.SelectionStart = _selectionStart;
            input.SelectionEnd = _selectionEnd;
        }
        else if (_target is SelectableTextBlock text && text.Text == _selectionText)
        {
            text.SelectionStart = _selectionStart;
            text.SelectionEnd = _selectionEnd;
        }
    }

    private void HandleTargetDetached(object? sender, VisualTreeAttachmentEventArgs args) => SetTarget(null);

    private void HandleWindowClosed(object? sender, EventArgs args) => Dispose();
}
