using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace MossAgent.App.Controls;

public sealed class AppPageSwitcher : AppUserControl
{
    public static readonly StyledProperty<Control?> PrimaryPageProperty =
        AvaloniaProperty.Register<AppPageSwitcher, Control?>(nameof(PrimaryPage));
    public static readonly StyledProperty<Control?> SecondaryPageProperty =
        AvaloniaProperty.Register<AppPageSwitcher, Control?>(nameof(SecondaryPage));
    public static readonly StyledProperty<bool> IsSecondaryActiveProperty =
        AvaloniaProperty.Register<AppPageSwitcher, bool>(nameof(IsSecondaryActive));
    public static readonly StyledProperty<KeyboardNavigationMode> SecondaryTabNavigationProperty =
        AvaloniaProperty.Register<AppPageSwitcher, KeyboardNavigationMode>(nameof(SecondaryTabNavigation), KeyboardNavigationMode.Cycle);

    private readonly ContentPresenter _primaryPresenter;
    private readonly ContentPresenter _secondaryPresenter;
    private Control? _primaryFocus;
    private Control? _secondaryFocus;
    private int _focusRevision;

    public AppPageSwitcher()
    {
        _primaryPresenter = CreatePresenter();
        _secondaryPresenter = CreatePresenter();
        _secondaryPresenter.IsVisible = false;
        KeyboardNavigation.SetTabNavigation(_secondaryPresenter, SecondaryTabNavigation);
        Content = new Grid { Children = { _primaryPresenter, _secondaryPresenter } };
    }

    public Control? PrimaryPage
    {
        get => GetValue(PrimaryPageProperty);
        set => SetValue(PrimaryPageProperty, value);
    }

    public Control? SecondaryPage
    {
        get => GetValue(SecondaryPageProperty);
        set => SetValue(SecondaryPageProperty, value);
    }

    public bool IsSecondaryActive
    {
        get => GetValue(IsSecondaryActiveProperty);
        set => SetValue(IsSecondaryActiveProperty, value);
    }

    public KeyboardNavigationMode SecondaryTabNavigation
    {
        get => GetValue(SecondaryTabNavigationProperty);
        set => SetValue(SecondaryTabNavigationProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_primaryPresenter is null) return;
        if (change.Property == SecondaryTabNavigationProperty)
        {
            KeyboardNavigation.SetTabNavigation(_secondaryPresenter, SecondaryTabNavigation);
        }
        else if (change.Property == PrimaryPageProperty)
        {
            _primaryFocus = null;
            _primaryPresenter.Content = PrimaryPage;
            if (!IsSecondaryActive) ScheduleFocus();
        }
        else if (change.Property == SecondaryPageProperty)
        {
            _secondaryFocus = null;
            _secondaryPresenter.Content = SecondaryPage;
            if (IsSecondaryActive) ScheduleFocus();
        }
        else if (change.Property == IsSecondaryActiveProperty)
        {
            var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
            if (IsSecondaryActive) _primaryFocus = focused;
            else _secondaryFocus = focused;
            _primaryPresenter.IsVisible = !IsSecondaryActive;
            _secondaryPresenter.IsVisible = IsSecondaryActive;
            ScheduleFocus();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs args)
    {
        base.OnAttachedToVisualTree(args);
        ScheduleFocus();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        _focusRevision++;
        _primaryFocus = null;
        _secondaryFocus = null;
        base.OnDetachedFromVisualTree(args);
    }

    internal static Control? SelectFocusTarget(Control page, Control? remembered)
    {
        if (remembered is not null && IsAvailableFocusTarget(remembered)
            && (ReferenceEquals(page, remembered) || page.IsVisualAncestorOf(remembered))) return remembered;

        Control? fallback = null;
        foreach (var visual in page.GetVisualDescendants().Prepend(page))
        {
            if (visual is not Control control || !IsAvailableFocusTarget(control)) continue;
            fallback ??= control;
            if (control is AppInput or AppComboBox or AppNumericUpDown) return control;
        }
        return fallback;
    }

    private void ScheduleFocus()
    {
        var revision = ++_focusRevision;
        if (TopLevel.GetTopLevel(this) is null) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (revision != _focusRevision || !IsEffectivelyVisible
                || TopLevel.GetTopLevel(this) is not { } topLevel) return;
            var page = IsSecondaryActive ? SecondaryPage : PrimaryPage;
            if (page is null) return;
            if (topLevel.FocusManager?.GetFocusedElement() is Control current
                && (ReferenceEquals(page, current) || page.IsVisualAncestorOf(current))) return;
            SelectFocusTarget(page, IsSecondaryActive ? _secondaryFocus : _primaryFocus)?.Focus();
        }, DispatcherPriority.Loaded);
    }

    private static ContentPresenter CreatePresenter() => new()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        VerticalContentAlignment = VerticalAlignment.Stretch
    };

    internal static bool IsAvailableFocusTarget(Control control)
    {
        if (!control.Focusable || !control.IsEffectivelyVisible || !control.IsEffectivelyEnabled) return false;
        foreach (var ancestor in control.GetVisualAncestors())
        {
            if (ancestor is Control parent && (!parent.IsVisible || !parent.IsEnabled)) return false;
            if (ancestor is ScrollContentPresenter viewport
                && control.TranslatePoint(default, viewport) is { } position
                && !new Rect(viewport.Bounds.Size).Contains(new Rect(position, control.Bounds.Size))) return false;
        }
        return true;
    }
}
