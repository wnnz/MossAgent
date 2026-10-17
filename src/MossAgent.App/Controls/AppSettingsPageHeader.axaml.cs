using Avalonia;
using Avalonia.Controls;

namespace MossAgent.App.Controls;

public sealed partial class AppSettingsPageHeader : AppUserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AppSettingsPageHeader, string?>(nameof(Title));
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<AppSettingsPageHeader, string?>(nameof(Description));
    public static readonly StyledProperty<string?> StatusProperty =
        AvaloniaProperty.Register<AppSettingsPageHeader, string?>(nameof(Status));
    public static readonly StyledProperty<bool> IsStatusErrorProperty =
        AvaloniaProperty.Register<AppSettingsPageHeader, bool>(nameof(IsStatusError));
    public static readonly StyledProperty<Control?> ActionsProperty =
        AvaloniaProperty.Register<AppSettingsPageHeader, Control?>(nameof(Actions));
    public static readonly DirectProperty<AppSettingsPageHeader, bool> HasDescriptionProperty =
        AvaloniaProperty.RegisterDirect<AppSettingsPageHeader, bool>(nameof(HasDescription), header => header.HasDescription);
    public static readonly DirectProperty<AppSettingsPageHeader, bool> HasStatusProperty =
        AvaloniaProperty.RegisterDirect<AppSettingsPageHeader, bool>(nameof(HasStatus), header => header.HasStatus);

    private bool _isStacked;

    static AppSettingsPageHeader() => AffectsMeasure<AppSettingsPageHeader>(ActionsProperty);

    public AppSettingsPageHeader()
    {
        Classes.Add("app-settings-page-header");
        InitializeComponent();
    }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string? Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
    public bool IsStatusError { get => GetValue(IsStatusErrorProperty); set => SetValue(IsStatusErrorProperty, value); }
    public Control? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = Bounds.Width > 0 ? Math.Min(Bounds.Width, availableSize.Width) : availableSize.Width;
        var stacked = width < 480 && Actions is not null;
        HeaderLayout.Classes.Set("has-actions", Actions is not null);
        if (_isStacked != stacked)
        {
            _isStacked = stacked;
            HeaderLayout.Classes.Set("stacked", stacked);
            Grid.SetRow(ActionsPresenter, stacked ? 1 : 0);
            Grid.SetColumn(ActionsPresenter, stacked ? 0 : 1);
            Grid.SetColumnSpan(ActionsPresenter, stacked ? 2 : 1);
        }
        Grid.SetColumnSpan(HeadingContent, stacked || Actions is null ? 2 : 1);
        return base.MeasureOverride(availableSize);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty && change.GetOldValue<Rect>().Width != Bounds.Width)
            InvalidateMeasure();
        if (change.Property == DescriptionProperty)
            RaisePropertyChanged(HasDescriptionProperty, !string.IsNullOrWhiteSpace(change.GetOldValue<string?>()), HasDescription);
        if (change.Property == StatusProperty)
            RaisePropertyChanged(HasStatusProperty, !string.IsNullOrWhiteSpace(change.GetOldValue<string?>()), HasStatus);
    }
}
