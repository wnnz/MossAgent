using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using MossAgent.App.Resources;

namespace MossAgent.App.Controls;

public sealed partial class AppEditableChoice : AppUserControl
{
    public static readonly StyledProperty<IReadOnlyList<string>> ChoicesProperty =
        AvaloniaProperty.Register<AppEditableChoice, IReadOnlyList<string>>(nameof(Choices), []);
    public static readonly StyledProperty<string?> SelectedChoiceProperty =
        AvaloniaProperty.Register<AppEditableChoice, string?>(nameof(SelectedChoice), defaultBindingMode: BindingMode.TwoWay);
    private bool _synchronizing;
    private AppFlyout ChoiceFlyout => (AppFlyout)ChoiceButton.Flyout!;

    public AppEditableChoice()
    {
        InitializeComponent();
        ChoiceButton.Label = ProviderSettingsText.Get("ChooseReasoningEffort");
        ToolTip.SetTip(ChoiceInput, ProviderSettingsText.Get("EditableReasoningHint"));
        ChoiceList.SelectionChanged += HandleSelectionChanged;
        ChoiceList.PointerReleased += (_, _) => ChoiceFlyout.Hide();
        ChoiceInput.KeyDown += HandleKeyDown;
        ChoiceFlyout.Opened += (_, _) => SynchronizeChoices();
    }

    public IReadOnlyList<string> Choices
    {
        get => GetValue(ChoicesProperty);
        set => SetValue(ChoicesProperty, value);
    }

    public string? SelectedChoice
    {
        get => GetValue(SelectedChoiceProperty);
        set => SetValue(SelectedChoiceProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (ChoiceList is not null && (change.Property == ChoicesProperty || change.Property == SelectedChoiceProperty))
            SynchronizeChoices();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        ChoiceFlyout.Hide();
        base.OnDetachedFromVisualTree(args);
    }

    private void SynchronizeChoices()
    {
        _synchronizing = true;
        try
        {
            ChoiceList.ItemsSource = Choices;
            ChoiceList.SelectedItem = Choices.Contains(SelectedChoice ?? string.Empty, StringComparer.Ordinal) ? SelectedChoice : null;
        }
        finally { _synchronizing = false; }
    }

    private void HandleSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_synchronizing || ChoiceList.SelectedItem is not string choice) return;
        SetCurrentValue(SelectedChoiceProperty, choice);
        ChoiceFlyout.Hide();
        ChoiceInput.Focus();
    }

    private void HandleKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.F4 || args.Key == Key.Down && args.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            if (ChoiceFlyout.IsOpen) ChoiceFlyout.Hide();
            else ChoiceFlyout.ShowAt(ChoiceButton);
        }
        else if (args.Key is Key.Up or Key.Down && Choices.Count > 0)
        {
            var index = Choices.ToList().IndexOf(SelectedChoice ?? string.Empty);
            index = Math.Clamp(index + (args.Key == Key.Up ? -1 : 1), 0, Choices.Count - 1);
            SetCurrentValue(SelectedChoiceProperty, Choices[index]);
        }
        else if (args.Key == Key.Escape && ChoiceFlyout.IsOpen) ChoiceFlyout.Hide();
        else return;
        args.Handled = true;
    }
}
