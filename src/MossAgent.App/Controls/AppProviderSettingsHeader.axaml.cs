using System.Windows.Input;
using Avalonia;
using MossAgent.App.Resources;
using MossAgent.Domain;

namespace MossAgent.App.Controls;

public sealed partial class AppProviderSettingsHeader : AppUserControl
{
    public static readonly StyledProperty<AiProvider?> ProviderProperty =
        AvaloniaProperty.Register<AppProviderSettingsHeader, AiProvider?>(nameof(Provider));
    public static readonly StyledProperty<int> ModelCountProperty =
        AvaloniaProperty.Register<AppProviderSettingsHeader, int>(nameof(ModelCount));
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<AppProviderSettingsHeader, bool>(nameof(IsExpanded));
    public static readonly StyledProperty<ICommand?> ToggleCommandProperty =
        AvaloniaProperty.Register<AppProviderSettingsHeader, ICommand?>(nameof(ToggleCommand));
    public static readonly StyledProperty<object?> ToggleCommandParameterProperty =
        AvaloniaProperty.Register<AppProviderSettingsHeader, object?>(nameof(ToggleCommandParameter));
    public static readonly StyledProperty<ICommand?> EditCommandProperty =
        AvaloniaProperty.Register<AppProviderSettingsHeader, ICommand?>(nameof(EditCommand));
    public static readonly DirectProperty<AppProviderSettingsHeader, string> ExpansionLabelProperty =
        AvaloniaProperty.RegisterDirect<AppProviderSettingsHeader, string>(nameof(ExpansionLabel), header => header.ExpansionLabel);
    public static readonly DirectProperty<AppProviderSettingsHeader, string> EditActionLabelProperty =
        AvaloniaProperty.RegisterDirect<AppProviderSettingsHeader, string>(nameof(EditActionLabel), header => header.EditActionLabel);
    public static readonly DirectProperty<AppProviderSettingsHeader, string> CountLabelProperty =
        AvaloniaProperty.RegisterDirect<AppProviderSettingsHeader, string>(nameof(CountLabel), header => header.CountLabel);
    public static readonly DirectProperty<AppProviderSettingsHeader, bool> HasToggleActionProperty =
        AvaloniaProperty.RegisterDirect<AppProviderSettingsHeader, bool>(nameof(HasToggleAction), header => header.HasToggleAction);
    public static readonly DirectProperty<AppProviderSettingsHeader, bool> HasEditActionProperty =
        AvaloniaProperty.RegisterDirect<AppProviderSettingsHeader, bool>(nameof(HasEditAction), header => header.HasEditAction);

    private bool _hasToggleAction;
    private bool _hasEditAction;

    public AppProviderSettingsHeader() => InitializeComponent();

    public AiProvider? Provider { get => GetValue(ProviderProperty); set => SetValue(ProviderProperty, value); }
    public int ModelCount { get => GetValue(ModelCountProperty); set => SetValue(ModelCountProperty, value); }
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public ICommand? ToggleCommand { get => GetValue(ToggleCommandProperty); set => SetValue(ToggleCommandProperty, value); }
    public object? ToggleCommandParameter { get => GetValue(ToggleCommandParameterProperty); set => SetValue(ToggleCommandParameterProperty, value); }
    public ICommand? EditCommand { get => GetValue(EditCommandProperty); set => SetValue(EditCommandProperty, value); }
    public string ExpansionLabel => ExpansionText(Provider, IsExpanded);
    public string EditActionLabel => EditText(Provider);
    public string CountLabel => ProviderSettingsText.Format("ModelCount", ModelCount);
    public string EditLabel => ProviderSettingsText.Get("Edit");
    public string DefaultLabel => ProviderSettingsText.Get("Default");
    public string DisabledLabel => ProviderSettingsText.Get("Disabled");
    public bool HasToggleAction => _hasToggleAction;
    public bool HasEditAction => _hasEditAction;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ProviderProperty)
        {
            var previous = change.GetOldValue<AiProvider?>();
            RaisePropertyChanged(ExpansionLabelProperty, ExpansionText(previous, IsExpanded), ExpansionLabel);
            RaisePropertyChanged(EditActionLabelProperty, EditText(previous), EditActionLabel);
        }
        if (change.Property == IsExpandedProperty)
            RaisePropertyChanged(ExpansionLabelProperty, ExpansionText(Provider, change.GetOldValue<bool>()), ExpansionLabel);
        if (change.Property == ModelCountProperty)
            RaisePropertyChanged(CountLabelProperty, ProviderSettingsText.Format("ModelCount", change.GetOldValue<int>()), CountLabel);
        if (change.Property == ProviderProperty || change.Property == ToggleCommandProperty
            || change.Property == ToggleCommandParameterProperty || change.Property == EditCommandProperty)
        {
            SetAndRaise(HasToggleActionProperty, ref _hasToggleAction,
                Provider is not null && ToggleCommand is not null && ToggleCommandParameter is not null);
            SetAndRaise(HasEditActionProperty, ref _hasEditAction, Provider is not null && EditCommand is not null);
        }
    }

    private static string ExpansionText(AiProvider? provider, bool expanded) =>
        ProviderSettingsText.Format(expanded ? "CollapseModels" : "ExpandModels", provider?.Name ?? string.Empty);

    private static string EditText(AiProvider? provider) =>
        ProviderSettingsText.Format("EditProvider", provider?.Name ?? string.Empty);
}
