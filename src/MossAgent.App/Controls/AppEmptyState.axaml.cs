using Avalonia;
using Avalonia.Media;

namespace MossAgent.App.Controls;

public sealed partial class AppEmptyState : AppUserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AppEmptyState, string?>(nameof(Title));
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<AppEmptyState, string?>(nameof(Description));
    public static readonly StyledProperty<Geometry?> IconDataProperty =
        AvaloniaProperty.Register<AppEmptyState, Geometry?>(nameof(IconData));
    public static readonly StyledProperty<bool> HasDescriptionProperty =
        AvaloniaProperty.Register<AppEmptyState, bool>(nameof(HasDescription));

    public AppEmptyState()
    {
        Classes.Add("app-empty-state");
        InitializeComponent();
    }
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public Geometry? IconData { get => GetValue(IconDataProperty); set => SetValue(IconDataProperty, value); }
    public bool HasDescription => GetValue(HasDescriptionProperty);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty || change.Property == DescriptionProperty)
            SetValue(HasDescriptionProperty, !string.IsNullOrWhiteSpace(Description) && Description != Title);
    }
}
