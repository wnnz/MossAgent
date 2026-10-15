using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.ViewModels;

namespace MossAgent.App.Controls;

public sealed partial class AppModelPickerMenu : AppUserControl
{
    private ModelPickerViewModel? _picker;
    private bool _attached;
    private int _focusRevision;

    public AppModelPickerMenu() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs args)
    {
        base.OnAttachedToVisualTree(args);
        _attached = true;
        ObservePicker();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        _attached = false;
        _focusRevision++;
        if (_picker is not null) _picker.PropertyChanged -= HandlePickerChanged;
        _picker = null;
        base.OnDetachedFromVisualTree(args);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (!_attached) return;
        if (change.Property == DataContextProperty) ObservePicker();
        else if (change.Property == IsVisibleProperty || change.Property == IsEnabledProperty) ScheduleFocus();
    }

    private void ObservePicker()
    {
        _focusRevision++;
        if (_picker is not null) _picker.PropertyChanged -= HandlePickerChanged;
        _picker = DataContext as ModelPickerViewModel;
        if (_picker is not null) _picker.PropertyChanged += HandlePickerChanged;
        ScheduleFocus();
    }

    private void HandlePickerChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (!ReferenceEquals(sender, _picker)) return;
        if (args.PropertyName is nameof(ModelPickerViewModel.IsPickerOpen)
            or nameof(ModelPickerViewModel.IsModelListVisible)
            or nameof(ModelPickerViewModel.CanChooseModel)
            or nameof(ModelPickerViewModel.ShowSearch)) ScheduleFocus();
    }

    private void ScheduleFocus()
    {
        var revision = ++_focusRevision;
        var picker = _picker;
        if (!_attached || picker?.IsPickerOpen != true) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (revision != _focusRevision || !_attached || !ReferenceEquals(picker, _picker)
                || !ReferenceEquals(picker, DataContext) || !picker.IsPickerOpen
                || !IsEffectivelyVisible || !IsEffectivelyEnabled) return;
            SelectFocusTarget(picker)?.Focus();
        }, DispatcherPriority.Loaded);
    }

    private Control? SelectFocusTarget(ModelPickerViewModel picker)
    {
        if (!picker.IsModelListVisible) return IsAvailable(QuickNavigationButton) ? QuickNavigationButton : null;
        if (IsAvailable(ModelSearchInput)) return ModelSearchInput;
        var buttons = this.GetVisualDescendants().OfType<AppButton>().Where(IsAvailable).ToArray();
        var models = buttons.OfType<AppActionButton>().Where(button => button.Classes.Contains("model-option"));
        return models.FirstOrDefault(button => button.IsTrailingIconVisible)
            ?? models.FirstOrDefault() ?? buttons.FirstOrDefault();
    }

    private static bool IsAvailable(Control control) => control.Focusable
        && control.IsEffectivelyVisible && control.IsEffectivelyEnabled
        && control.GetVisualAncestors().OfType<Control>().All(parent => parent.IsVisible && parent.IsEnabled);
}
