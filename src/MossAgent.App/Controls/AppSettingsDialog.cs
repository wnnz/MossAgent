using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Resources;

namespace MossAgent.App.Controls;

public sealed class AppSettingsDialog : ContentControl
{
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<AppSettingsDialog, bool>(nameof(IsOpen));
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AppSettingsDialog, string?>(nameof(Title));
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<AppSettingsDialog, object?>(nameof(Footer));
    public static readonly StyledProperty<ICommand?> CloseCommandProperty =
        AvaloniaProperty.Register<AppSettingsDialog, ICommand?>(nameof(CloseCommand));
    public static readonly StyledProperty<string> CloseLabelProperty =
        AvaloniaProperty.Register<AppSettingsDialog, string>(nameof(CloseLabel));
    public static readonly StyledProperty<bool> IsCloseButtonVisibleProperty =
        AvaloniaProperty.Register<AppSettingsDialog, bool>(nameof(IsCloseButtonVisible), true);

    private AppCard? _surface;
    private AppScrollViewer? _body;
    private IInputElement? _previousFocus;
    private Control? _dialogFocus;
    private int _focusRevision;
    private Control[] _focusHosts = [];

    public AppSettingsDialog()
    {
        IsVisible = false;
        CloseLabel = SettingsText.Get("CloseDialog");
    }

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    public string CloseLabel
    {
        get => GetValue(CloseLabelProperty);
        set => SetValue(CloseLabelProperty, value);
    }

    public bool IsCloseButtonVisible
    {
        get => GetValue(IsCloseButtonVisibleProperty);
        set => SetValue(IsCloseButtonVisibleProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs args)
    {
        base.OnApplyTemplate(args);
        _surface = args.NameScope.Find<AppCard>("PART_Dialog");
        _body = args.NameScope.Find<AppScrollViewer>("PART_DialogBody");
        _dialogFocus = null;
        if (IsOpen) ScheduleFocus();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty || change.Property == IsEnabledProperty)
        {
            ScheduleFocus();
            return;
        }
        if (change.Property != IsOpenProperty) return;
        if (IsOpen)
        {
            _previousFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            _dialogFocus = null;
            if (_body is not null) _body.Offset = default;
            IsVisible = true;
            ScheduleFocus();
        }
        else
        {
            IsVisible = false;
            RestorePreviousFocus();
            _dialogFocus = null;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs args)
    {
        base.OnAttachedToVisualTree(args);
        _focusHosts = this.GetVisualAncestors().OfType<Control>().ToArray();
        foreach (var host in _focusHosts) host.PropertyChanged += HandleHostStateChanged;
        if (IsOpen) ScheduleFocus();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        foreach (var host in _focusHosts) host.PropertyChanged -= HandleHostStateChanged;
        _focusHosts = [];
        _focusRevision++;
        _previousFocus = null;
        _dialogFocus = null;
        base.OnDetachedFromVisualTree(args);
    }

    protected override void OnGotFocus(FocusChangedEventArgs args)
    {
        base.OnGotFocus(args);
        if (args.Source is Control focused && _surface?.IsVisualAncestorOf(focused) == true)
            _dialogFocus = focused;
    }

    protected override void OnKeyDown(KeyEventArgs args)
    {
        base.OnKeyDown(args);
        if (!args.Handled && IsOpen && args.Key == Key.Escape && CloseCommand?.CanExecute(null) == true)
        {
            CloseCommand.Execute(null);
            args.Handled = true;
        }
    }

    private void HandleHostStateChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property == IsVisibleProperty || args.Property == IsEnabledProperty) ScheduleFocus();
    }

    private void RestorePreviousFocus()
    {
        var revision = ++_focusRevision;
        var previous = _previousFocus as Control;
        var owner = TopLevel.GetTopLevel(this);
        _previousFocus = null;
        if (previous is null || owner is null || !ReferenceEquals(TopLevel.GetTopLevel(previous), owner)) return;
        if (previous.IsEffectivelyVisible && previous.IsEffectivelyEnabled && previous.Focus()) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (revision != _focusRevision || IsOpen || !ReferenceEquals(TopLevel.GetTopLevel(this), owner)
                || !ReferenceEquals(TopLevel.GetTopLevel(previous), owner)
                || !previous.IsEffectivelyVisible || !previous.IsEffectivelyEnabled
                || owner.FocusManager?.GetFocusedElement() is not null) return;
            previous.Focus();
        }, DispatcherPriority.Loaded);
    }

    private void ScheduleFocus()
    {
        var revision = ++_focusRevision;
        if (!IsOpen) return;
        Dispatcher.UIThread.Post(() =>
        {
            var surface = _surface;
            if (revision != _focusRevision || !IsOpen || !IsEffectivelyVisible || !IsEffectivelyEnabled
                || TopLevel.GetTopLevel(this) is not { } topLevel || surface is null) return;
            if (topLevel.FocusManager?.GetFocusedElement() is Control current
                && surface.IsVisualAncestorOf(current)) return;
            var controls = surface.GetVisualDescendants().OfType<Control>()
                .Where(control => control.Focusable && control.IsEffectivelyVisible && control.IsEffectivelyEnabled)
                .ToArray();
            var field = controls.FirstOrDefault(control => ReferenceEquals(control, _dialogFocus))
                ?? controls.FirstOrDefault(control => control is AppInput or AppComboBox or AppNumericUpDown);
            (field ?? controls.FirstOrDefault())?.Focus();
        }, DispatcherPriority.Loaded);
    }
}
