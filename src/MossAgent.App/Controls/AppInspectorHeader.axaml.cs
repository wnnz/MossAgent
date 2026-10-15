using System.Windows.Input;
using Avalonia;
using MossAgent.App.Resources;

namespace MossAgent.App.Controls;

public sealed partial class AppInspectorHeader : AppUserControl
{
    private readonly AppIconButton[] _tabs = [];

    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<AppInspectorHeader, string?>(nameof(Title));
    public static readonly StyledProperty<string?> ActiveTabProperty = AvaloniaProperty.Register<AppInspectorHeader, string?>(nameof(ActiveTab), "home");
    public static readonly StyledProperty<string?> CloseLabelProperty = AvaloniaProperty.Register<AppInspectorHeader, string?>(nameof(CloseLabel));
    public static readonly StyledProperty<ICommand?> SelectCommandProperty = AvaloniaProperty.Register<AppInspectorHeader, ICommand?>(nameof(SelectCommand));
    public static readonly StyledProperty<ICommand?> BackCommandProperty = AvaloniaProperty.Register<AppInspectorHeader, ICommand?>(nameof(BackCommand));
    public static readonly StyledProperty<ICommand?> CloseCommandProperty = AvaloniaProperty.Register<AppInspectorHeader, ICommand?>(nameof(CloseCommand));
    public static readonly StyledProperty<ICommand?> ExpansionCommandProperty = AvaloniaProperty.Register<AppInspectorHeader, ICommand?>(nameof(ExpansionCommand));
    public static readonly StyledProperty<bool> IsExpandedProperty = AvaloniaProperty.Register<AppInspectorHeader, bool>(nameof(IsExpanded));

    public AppInspectorHeader()
    {
        InitializeComponent();
        _tabs = [SourcesButton, DiffButton, TerminalButton, BrowserButton, AuditButton];
        UpdateState();
    }
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? ActiveTab { get => GetValue(ActiveTabProperty); set => SetValue(ActiveTabProperty, value); }
    public string? CloseLabel { get => GetValue(CloseLabelProperty); set => SetValue(CloseLabelProperty, value); }
    public ICommand? SelectCommand { get => GetValue(SelectCommandProperty); set => SetValue(SelectCommandProperty, value); }
    public ICommand? BackCommand { get => GetValue(BackCommandProperty); set => SetValue(BackCommandProperty, value); }
    public ICommand? CloseCommand { get => GetValue(CloseCommandProperty); set => SetValue(CloseCommandProperty, value); }
    public ICommand? ExpansionCommand { get => GetValue(ExpansionCommandProperty); set => SetValue(ExpansionCommandProperty, value); }
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_tabs is not { Length: > 0 }) return;
        if (change.Property == ActiveTabProperty || change.Property == SelectCommandProperty
            || change.Property == BackCommandProperty || change.Property == CloseCommandProperty
            || change.Property == ExpansionCommandProperty || change.Property == IsExpandedProperty)
            UpdateState();
    }

    private void UpdateState()
    {
        var activeTab = ActiveTab == "home" ? "sources" : ActiveTab;
        var canSelect = SelectCommand is not null;
        foreach (var button in _tabs)
        {
            button.Classes.Set("selected", Equals(button.CommandParameter, activeTab));
            button.IsEnabled = canSelect;
        }
        BackButton.IsVisible = activeTab != "sources";
        BackButton.IsEnabled = BackCommand is not null;
        CloseButton.IsEnabled = CloseCommand is not null;
        ExpansionButton.IsVisible = ExpansionCommand is not null;
        ExpansionButton.IsEnabled = ExpansionCommand is not null;
        ExpansionButton.Label = IsExpanded ? InspectorText.PreviewRestore : InspectorText.PreviewExpand;
    }
}
