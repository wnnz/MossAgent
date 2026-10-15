using Avalonia;
using Avalonia.Data;

namespace MossAgent.App.Controls;

public sealed class AppChoiceComboBox : AppComboBox
{
    public static readonly StyledProperty<IReadOnlyList<string>> ChoicesProperty =
        AvaloniaProperty.Register<AppChoiceComboBox, IReadOnlyList<string>>(nameof(Choices), []);
    public static readonly StyledProperty<string?> SelectedChoiceProperty =
        AvaloniaProperty.Register<AppChoiceComboBox, string?>(nameof(SelectedChoice), defaultBindingMode: BindingMode.TwoWay);
    private bool _synchronizing;

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
        if (change.Property == ChoicesProperty || change.Property == SelectedChoiceProperty) SynchronizeSelection();
        else if (change.Property == SelectedItemProperty && !_synchronizing && SelectedItem is string choice)
            SetCurrentValue(SelectedChoiceProperty, choice);
    }

    private void SynchronizeSelection()
    {
        if (_synchronizing) return;
        _synchronizing = true;
        try
        {
            ItemsSource = Choices;
            SetCurrentValue(SelectedItemProperty, Choices.Contains(SelectedChoice ?? string.Empty, StringComparer.Ordinal)
                ? SelectedChoice : null);
        }
        finally { _synchronizing = false; }
    }
}
