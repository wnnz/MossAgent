using System.Windows.Input;
using Avalonia;
using MossAgent.App.Resources;

namespace MossAgent.App.Controls;

public sealed partial class AppSettingsItemRow : AppUserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AppSettingsItemRow, string?>(nameof(Title));
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<AppSettingsItemRow, string?>(nameof(Subtitle));
    public static readonly StyledProperty<string?> MetadataProperty =
        AvaloniaProperty.Register<AppSettingsItemRow, string?>(nameof(Metadata));
    public static readonly StyledProperty<string?> BadgeTextProperty =
        AvaloniaProperty.Register<AppSettingsItemRow, string?>(nameof(BadgeText));
    public static readonly StyledProperty<bool> IsBadgeVisibleProperty =
        AvaloniaProperty.Register<AppSettingsItemRow, bool>(nameof(IsBadgeVisible), true);
    public static readonly StyledProperty<string?> PrimaryLabelProperty =
        AvaloniaProperty.Register<AppSettingsItemRow, string?>(nameof(PrimaryLabel));
    public static readonly StyledProperty<string?> AlternatePrimaryLabelProperty =
        AvaloniaProperty.Register<AppSettingsItemRow, string?>(nameof(AlternatePrimaryLabel));
    public static readonly StyledProperty<bool> IsPrimaryAlternateProperty =
        AvaloniaProperty.Register<AppSettingsItemRow, bool>(nameof(IsPrimaryAlternate));
    public static readonly StyledProperty<string?> SecondaryLabelProperty =
        AvaloniaProperty.Register<AppSettingsItemRow, string?>(nameof(SecondaryLabel));
    public static readonly StyledProperty<ICommand?> PrimaryCommandProperty =
        AvaloniaProperty.Register<AppSettingsItemRow, ICommand?>(nameof(PrimaryCommand));
    public static readonly StyledProperty<ICommand?> SecondaryCommandProperty =
        AvaloniaProperty.Register<AppSettingsItemRow, ICommand?>(nameof(SecondaryCommand));
    public static readonly StyledProperty<object?> ActionTargetProperty =
        AvaloniaProperty.Register<AppSettingsItemRow, object?>(nameof(ActionTarget));

    public static readonly DirectProperty<AppSettingsItemRow, string> PrimaryActionLabelProperty =
        AvaloniaProperty.RegisterDirect<AppSettingsItemRow, string>(nameof(PrimaryActionLabel), row => row.PrimaryActionLabel);
    public static readonly DirectProperty<AppSettingsItemRow, string> PrimaryActionNameProperty =
        AvaloniaProperty.RegisterDirect<AppSettingsItemRow, string>(nameof(PrimaryActionName), row => row.PrimaryActionName);
    public static readonly DirectProperty<AppSettingsItemRow, string> SecondaryActionNameProperty =
        AvaloniaProperty.RegisterDirect<AppSettingsItemRow, string>(nameof(SecondaryActionName), row => row.SecondaryActionName);
    public static readonly DirectProperty<AppSettingsItemRow, bool> HasSubtitleProperty =
        AvaloniaProperty.RegisterDirect<AppSettingsItemRow, bool>(nameof(HasSubtitle), row => row.HasSubtitle);
    public static readonly DirectProperty<AppSettingsItemRow, bool> HasMetadataProperty =
        AvaloniaProperty.RegisterDirect<AppSettingsItemRow, bool>(nameof(HasMetadata), row => row.HasMetadata);
    public static readonly DirectProperty<AppSettingsItemRow, bool> HasBadgeProperty =
        AvaloniaProperty.RegisterDirect<AppSettingsItemRow, bool>(nameof(HasBadge), row => row.HasBadge);
    public static readonly DirectProperty<AppSettingsItemRow, bool> HasPrimaryActionProperty =
        AvaloniaProperty.RegisterDirect<AppSettingsItemRow, bool>(nameof(HasPrimaryAction), row => row.HasPrimaryAction);
    public static readonly DirectProperty<AppSettingsItemRow, bool> HasSecondaryActionProperty =
        AvaloniaProperty.RegisterDirect<AppSettingsItemRow, bool>(nameof(HasSecondaryAction), row => row.HasSecondaryAction);
    public static readonly DirectProperty<AppSettingsItemRow, bool> IsPrimaryAvailableProperty =
        AvaloniaProperty.RegisterDirect<AppSettingsItemRow, bool>(nameof(IsPrimaryAvailable), row => row.IsPrimaryAvailable);
    public static readonly DirectProperty<AppSettingsItemRow, bool> IsSecondaryAvailableProperty =
        AvaloniaProperty.RegisterDirect<AppSettingsItemRow, bool>(nameof(IsSecondaryAvailable), row => row.IsSecondaryAvailable);

    private string _primaryActionLabel = string.Empty;
    private string _primaryActionName = string.Empty;
    private string _secondaryActionName = string.Empty;
    private bool _hasSubtitle;
    private bool _hasMetadata;
    private bool _hasBadge;
    private bool _hasPrimaryAction;
    private bool _hasSecondaryAction;
    private bool _isPrimaryAvailable;
    private bool _isSecondaryAvailable;

    public AppSettingsItemRow()
    {
        Classes.Add("app-settings-item-row");
        InitializeComponent();
    }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public string? Metadata { get => GetValue(MetadataProperty); set => SetValue(MetadataProperty, value); }
    public string? BadgeText { get => GetValue(BadgeTextProperty); set => SetValue(BadgeTextProperty, value); }
    public bool IsBadgeVisible { get => GetValue(IsBadgeVisibleProperty); set => SetValue(IsBadgeVisibleProperty, value); }
    public string? PrimaryLabel { get => GetValue(PrimaryLabelProperty); set => SetValue(PrimaryLabelProperty, value); }
    public string? AlternatePrimaryLabel { get => GetValue(AlternatePrimaryLabelProperty); set => SetValue(AlternatePrimaryLabelProperty, value); }
    public bool IsPrimaryAlternate { get => GetValue(IsPrimaryAlternateProperty); set => SetValue(IsPrimaryAlternateProperty, value); }
    public string? SecondaryLabel { get => GetValue(SecondaryLabelProperty); set => SetValue(SecondaryLabelProperty, value); }
    public ICommand? PrimaryCommand { get => GetValue(PrimaryCommandProperty); set => SetValue(PrimaryCommandProperty, value); }
    public ICommand? SecondaryCommand { get => GetValue(SecondaryCommandProperty); set => SetValue(SecondaryCommandProperty, value); }
    public object? ActionTarget { get => GetValue(ActionTargetProperty); set => SetValue(ActionTargetProperty, value); }
    public string PrimaryActionLabel => _primaryActionLabel;
    public string PrimaryActionName => _primaryActionName;
    public string SecondaryActionName => _secondaryActionName;
    public bool HasSubtitle => _hasSubtitle;
    public bool HasMetadata => _hasMetadata;
    public bool HasBadge => _hasBadge;
    public bool HasPrimaryAction => _hasPrimaryAction;
    public bool HasSecondaryAction => _hasSecondaryAction;
    public bool IsPrimaryAvailable => _isPrimaryAvailable;
    public bool IsSecondaryAvailable => _isSecondaryAvailable;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty || change.Property == SubtitleProperty
            || change.Property == MetadataProperty || change.Property == BadgeTextProperty
            || change.Property == IsBadgeVisibleProperty || change.Property == PrimaryLabelProperty
            || change.Property == AlternatePrimaryLabelProperty || change.Property == IsPrimaryAlternateProperty
            || change.Property == SecondaryLabelProperty || change.Property == PrimaryCommandProperty
            || change.Property == SecondaryCommandProperty || change.Property == ActionTargetProperty)
            UpdatePresentation();
    }

    private void UpdatePresentation()
    {
        var label = IsPrimaryAlternate ? AlternatePrimaryLabel : PrimaryLabel;
        SetAndRaise(PrimaryActionLabelProperty, ref _primaryActionLabel, label ?? string.Empty);
        SetAndRaise(PrimaryActionNameProperty, ref _primaryActionName, SettingsItemLabels.ActionName(label, Title));
        SetAndRaise(SecondaryActionNameProperty, ref _secondaryActionName, SettingsItemLabels.ActionName(SecondaryLabel, Title));
        SetAndRaise(HasSubtitleProperty, ref _hasSubtitle, !string.IsNullOrWhiteSpace(Subtitle));
        SetAndRaise(HasMetadataProperty, ref _hasMetadata, !string.IsNullOrWhiteSpace(Metadata));
        SetAndRaise(HasBadgeProperty, ref _hasBadge, IsBadgeVisible && !string.IsNullOrWhiteSpace(BadgeText));
        SetAndRaise(HasPrimaryActionProperty, ref _hasPrimaryAction, !string.IsNullOrWhiteSpace(label));
        SetAndRaise(HasSecondaryActionProperty, ref _hasSecondaryAction, !string.IsNullOrWhiteSpace(SecondaryLabel));
        SetAndRaise(IsPrimaryAvailableProperty, ref _isPrimaryAvailable,
            HasPrimaryAction && PrimaryCommand is not null && ActionTarget is not null);
        SetAndRaise(IsSecondaryAvailableProperty, ref _isSecondaryAvailable,
            HasSecondaryAction && SecondaryCommand is not null && ActionTarget is not null);
    }
}
