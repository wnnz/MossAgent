using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace MossAgent.App.Controls;

public partial class AppDiscreteSlider : AppUserControl
{
    public static readonly StyledProperty<IReadOnlyList<string>> StepValuesProperty =
        AvaloniaProperty.Register<AppDiscreteSlider, IReadOnlyList<string>>(nameof(StepValues), []);
    public static readonly StyledProperty<string?> SelectedStepProperty =
        AvaloniaProperty.Register<AppDiscreteSlider, string?>(nameof(SelectedStep), defaultBindingMode: BindingMode.TwoWay);
    private readonly AppSlider _slider;
    private bool _synchronizing;

    public AppDiscreteSlider()
    {
        InitializeComponent();
        _slider = this.FindControl<AppSlider>("StepSlider")!;
        SynchronizeSlider();
    }

    public IReadOnlyList<string> StepValues
    {
        get => GetValue(StepValuesProperty);
        set => SetValue(StepValuesProperty, value);
    }

    public string? SelectedStep
    {
        get => GetValue(SelectedStepProperty);
        set => SetValue(SelectedStepProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs args)
    {
        base.OnAttachedToVisualTree(args);
        _slider.PropertyChanged += HandleSliderChanged;
        AddHandler(KeyDownEvent, HandleKeyInput, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, HandlePointerInput, RoutingStrategies.Bubble, handledEventsToo: true);
        SynchronizeSlider();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        _slider.PropertyChanged -= HandleSliderChanged;
        RemoveHandler(KeyDownEvent, HandleKeyInput);
        RemoveHandler(PointerReleasedEvent, HandlePointerInput);
        base.OnDetachedFromVisualTree(args);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_slider is not null && (change.Property == StepValuesProperty || change.Property == SelectedStepProperty))
            SynchronizeSlider();
    }

    private void SynchronizeSlider()
    {
        if (_synchronizing) return;
        _synchronizing = true;
        try
        {
            var steps = StepValues;
            _slider.Maximum = Math.Max(0, steps.Count - 1);
            _slider.IsEnabled = steps.Count > 1;
            _slider.Value = Math.Max(0, steps.ToList().IndexOf(SelectedStep ?? string.Empty));
        }
        finally { _synchronizing = false; }
    }

    private void HandleSliderChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == AppSlider.ValueProperty) CommitStep();
    }

    private void HandleKeyInput(object? sender, KeyEventArgs args)
    {
        if (args.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown)
            CommitStep();
    }

    private void HandlePointerInput(object? sender, PointerReleasedEventArgs args)
    {
        if (args.InitialPressMouseButton == MouseButton.Left) CommitStep();
    }

    private void CommitStep()
    {
        if (_synchronizing || !_slider.IsEffectivelyEnabled || StepValues.Count == 0) return;
        var index = Math.Clamp((int)Math.Round(_slider.Value), 0, StepValues.Count - 1);
        SetCurrentValue(SelectedStepProperty, StepValues[index]);
    }
}
