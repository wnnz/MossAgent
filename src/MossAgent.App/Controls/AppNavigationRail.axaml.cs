using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using MossAgent.App.Resources;

namespace MossAgent.App.Controls;

public sealed partial class AppNavigationRail : AppUserControl
{
    private readonly List<Visual> _ancestors = [];

    public static readonly StyledProperty<ICommand?> NewConversationCommandProperty =
        AvaloniaProperty.Register<AppNavigationRail, ICommand?>(nameof(NewConversationCommand));
    public static readonly StyledProperty<ICommand?> InspectorCommandProperty =
        AvaloniaProperty.Register<AppNavigationRail, ICommand?>(nameof(InspectorCommand));
    public static readonly StyledProperty<ICommand?> AutomationsCommandProperty =
        AvaloniaProperty.Register<AppNavigationRail, ICommand?>(nameof(AutomationsCommand));
    public static readonly StyledProperty<ICommand?> PullRequestsCommandProperty =
        AvaloniaProperty.Register<AppNavigationRail, ICommand?>(nameof(PullRequestsCommand));
    public static readonly StyledProperty<bool> IsPullRequestsSelectedProperty =
        AvaloniaProperty.Register<AppNavigationRail, bool>(nameof(IsPullRequestsSelected));
    public static readonly StyledProperty<bool> IsAutomationsSelectedProperty =
        AvaloniaProperty.Register<AppNavigationRail, bool>(nameof(IsAutomationsSelected));
    public static readonly StyledProperty<bool> IsSettingsSelectedProperty =
        AvaloniaProperty.Register<AppNavigationRail, bool>(nameof(IsSettingsSelected));
    public static readonly StyledProperty<ICommand?> ToggleInspectorCommandProperty =
        AvaloniaProperty.Register<AppNavigationRail, ICommand?>(nameof(ToggleInspectorCommand));
    public static readonly StyledProperty<ICommand?> SettingsCommandProperty =
        AvaloniaProperty.Register<AppNavigationRail, ICommand?>(nameof(SettingsCommand));
    public static readonly StyledProperty<Control?> HelpContentProperty =
        AvaloniaProperty.Register<AppNavigationRail, Control?>(nameof(HelpContent));

    public AppNavigationRail()
    {
        Classes.Add("app-navigation-rail");
        InitializeComponent();
        UpdateAvailability();
    }

    public ICommand? NewConversationCommand { get => GetValue(NewConversationCommandProperty); set => SetValue(NewConversationCommandProperty, value); }
    public ICommand? InspectorCommand { get => GetValue(InspectorCommandProperty); set => SetValue(InspectorCommandProperty, value); }
    public ICommand? AutomationsCommand { get => GetValue(AutomationsCommandProperty); set => SetValue(AutomationsCommandProperty, value); }
    public ICommand? PullRequestsCommand { get => GetValue(PullRequestsCommandProperty); set => SetValue(PullRequestsCommandProperty, value); }
    public bool IsPullRequestsSelected { get => GetValue(IsPullRequestsSelectedProperty); set => SetValue(IsPullRequestsSelectedProperty, value); }
    public string PullRequestsLabel => PullRequestText.Title;
    public bool IsAutomationsSelected { get => GetValue(IsAutomationsSelectedProperty); set => SetValue(IsAutomationsSelectedProperty, value); }
    public bool IsSettingsSelected { get => GetValue(IsSettingsSelectedProperty); set => SetValue(IsSettingsSelectedProperty, value); }
    public ICommand? ToggleInspectorCommand { get => GetValue(ToggleInspectorCommandProperty); set => SetValue(ToggleInspectorCommandProperty, value); }
    public ICommand? SettingsCommand { get => GetValue(SettingsCommandProperty); set => SetValue(SettingsCommandProperty, value); }
    public Control? HelpContent { get => GetValue(HelpContentProperty); set => SetValue(HelpContentProperty, value); }

    public string NewConversationLabel => NavigationRailText.Get("NewConversation");
    public string AutomationsLabel => NavigationRailText.Get("Automations");
    public string AutomationsUnavailableLabel => NavigationRailText.Get("AutomationsUnavailable");
    public string AuditLabel => NavigationRailText.Get("Audit");
    public string MoreLabel => NavigationRailText.Get("More");
    public string HelpLabel => NavigationRailText.Get("Help");
    public string SettingsLabel => NavigationRailText.Get("Settings");
    public string InspectorLabel => NavigationRailText.Get("ToggleInspector");

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (MoreButton is null) return;
        if (change.Property == NewConversationCommandProperty || change.Property == InspectorCommandProperty
            || change.Property == AutomationsCommandProperty || change.Property == IsAutomationsSelectedProperty
            || change.Property == IsSettingsSelectedProperty
            || change.Property == PullRequestsCommandProperty || change.Property == IsPullRequestsSelectedProperty
            || change.Property == ToggleInspectorCommandProperty || change.Property == SettingsCommandProperty
            || change.Property == HelpContentProperty || change.Property == DataContextProperty
            || (change.Property == IsVisibleProperty && !IsVisible)
            || (change.Property == IsEnabledProperty && !IsEnabled))
            CloseFlyouts();
        UpdateAvailability();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs args)
    {
        base.OnAttachedToVisualTree(args);
        foreach (var ancestor in this.GetVisualAncestors())
        {
            _ancestors.Add(ancestor);
            ancestor.PropertyChanged += HandleAncestorChanged;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        CloseFlyouts();
        foreach (var ancestor in _ancestors) ancestor.PropertyChanged -= HandleAncestorChanged;
        _ancestors.Clear();
        base.OnDetachedFromVisualTree(args);
    }

    private void HandleAncestorChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if ((args.Property == IsEnabledProperty || args.Property == IsVisibleProperty)
            && (!IsEffectivelyEnabled || !IsEffectivelyVisible)) CloseFlyouts();
    }

    private void CloseFlyouts()
    {
        MoreButton.Flyout?.Hide();
        HelpButton.Flyout?.Hide();
    }

    private void UpdateAvailability()
    {
        HomeButton.Label = NavigationRailText.Get(IsSettingsSelected ? "ReturnToWorkspace" : "NewConversation");
        HomeButton.Classes.Set("selected", !IsSettingsSelected && !IsAutomationsSelected && !IsPullRequestsSelected);
        PullRequestsButton.Classes.Set("selected", !IsSettingsSelected && IsPullRequestsSelected);
        PullRequestsButton.IsVisible = PullRequestsCommand is not null;
        AutomationButton.Classes.Set("selected", !IsSettingsSelected && IsAutomationsSelected);
        HomeButton.IsEnabled = NewConversationCommand is not null;
        AutomationButton.IsEnabled = AutomationsCommand is not null;
        AuditButton.IsEnabled = InspectorCommand is not null;
        SettingsButton.IsEnabled = SettingsCommand is not null;
        InspectorMenuButton.IsEnabled = ToggleInspectorCommand is not null;
        SettingsMenuButton.IsEnabled = SettingsCommand is not null;
        MoreButton.IsEnabled = ToggleInspectorCommand is not null || SettingsCommand is not null;
        HelpButton.IsEnabled = HelpContent is not null;
    }
}
