using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using MossAgent.App.Resources;

namespace MossAgent.App.Controls;

public sealed partial class AppConversationHeader : AppUserControl
{
    public static readonly StyledProperty<Control?> HeadingContentProperty =
        AvaloniaProperty.Register<AppConversationHeader, Control?>(nameof(HeadingContent));
    public static readonly StyledProperty<Control?> MenuContentProperty =
        AvaloniaProperty.Register<AppConversationHeader, Control?>(nameof(MenuContent));
    public static readonly StyledProperty<object?> ConversationTargetProperty =
        AvaloniaProperty.Register<AppConversationHeader, object?>(nameof(ConversationTarget));
    public static readonly StyledProperty<bool> IsContextVisibleProperty =
        AvaloniaProperty.Register<AppConversationHeader, bool>(nameof(IsContextVisible));
    public static readonly StyledProperty<bool> IsInspectorOpenProperty =
        AvaloniaProperty.Register<AppConversationHeader, bool>(nameof(IsInspectorOpen));
    public static readonly StyledProperty<string?> InspectorLabelProperty =
        AvaloniaProperty.Register<AppConversationHeader, string?>(nameof(InspectorLabel));
    public static readonly StyledProperty<ICommand?> ToggleContextCommandProperty =
        AvaloniaProperty.Register<AppConversationHeader, ICommand?>(nameof(ToggleContextCommand));
    public static readonly StyledProperty<ICommand?> ToggleInspectorCommandProperty =
        AvaloniaProperty.Register<AppConversationHeader, ICommand?>(nameof(ToggleInspectorCommand));
    public static readonly StyledProperty<ICommand?> ExpandInspectorCommandProperty =
        AvaloniaProperty.Register<AppConversationHeader, ICommand?>(nameof(ExpandInspectorCommand));
    public static readonly StyledProperty<string?> StartupStatusProperty =
        AvaloniaProperty.Register<AppConversationHeader, string?>(nameof(StartupStatus));
    public static readonly StyledProperty<bool> HasStartupStatusProperty =
        AvaloniaProperty.Register<AppConversationHeader, bool>(nameof(HasStartupStatus));
    public static readonly StyledProperty<bool> HasStartupWarningProperty =
        AvaloniaProperty.Register<AppConversationHeader, bool>(nameof(HasStartupWarning));
    public static readonly StyledProperty<string?> StartupActionLabelProperty =
        AvaloniaProperty.Register<AppConversationHeader, string?>(nameof(StartupActionLabel));
    public static readonly StyledProperty<ICommand?> StartupActionCommandProperty =
        AvaloniaProperty.Register<AppConversationHeader, ICommand?>(nameof(StartupActionCommand));
    public static readonly StyledProperty<string?> ActivityProperty =
        AvaloniaProperty.Register<AppConversationHeader, string?>(nameof(Activity));
    public static readonly StyledProperty<bool> IsRunningProperty =
        AvaloniaProperty.Register<AppConversationHeader, bool>(nameof(IsRunning));
    public static readonly DirectProperty<AppConversationHeader, bool> HasConversationProperty =
        AvaloniaProperty.RegisterDirect<AppConversationHeader, bool>(nameof(HasConversation), header => header.HasConversation);
    public static readonly DirectProperty<AppConversationHeader, bool> HasStatusProperty =
        AvaloniaProperty.RegisterDirect<AppConversationHeader, bool>(nameof(HasStatus), header => header.HasStatus);
    public static readonly DirectProperty<AppConversationHeader, bool> HasExpansionProperty =
        AvaloniaProperty.RegisterDirect<AppConversationHeader, bool>(nameof(HasExpansion), header => header.HasExpansion);

    private bool _hasConversation;
    private bool _hasStatus;
    private bool _hasExpansion;

    public AppConversationHeader()
    {
        Classes.Add("app-conversation-header");
        InitializeComponent();
        UpdateState();
    }

    public Control? HeadingContent { get => GetValue(HeadingContentProperty); set => SetValue(HeadingContentProperty, value); }
    public Control? MenuContent { get => GetValue(MenuContentProperty); set => SetValue(MenuContentProperty, value); }
    public object? ConversationTarget { get => GetValue(ConversationTargetProperty); set => SetValue(ConversationTargetProperty, value); }
    public bool IsContextVisible { get => GetValue(IsContextVisibleProperty); set => SetValue(IsContextVisibleProperty, value); }
    public bool IsInspectorOpen { get => GetValue(IsInspectorOpenProperty); set => SetValue(IsInspectorOpenProperty, value); }
    public string? InspectorLabel { get => GetValue(InspectorLabelProperty); set => SetValue(InspectorLabelProperty, value); }
    public ICommand? ToggleContextCommand { get => GetValue(ToggleContextCommandProperty); set => SetValue(ToggleContextCommandProperty, value); }
    public ICommand? ToggleInspectorCommand { get => GetValue(ToggleInspectorCommandProperty); set => SetValue(ToggleInspectorCommandProperty, value); }
    public ICommand? ExpandInspectorCommand { get => GetValue(ExpandInspectorCommandProperty); set => SetValue(ExpandInspectorCommandProperty, value); }
    public string? StartupStatus { get => GetValue(StartupStatusProperty); set => SetValue(StartupStatusProperty, value); }
    public bool HasStartupStatus { get => GetValue(HasStartupStatusProperty); set => SetValue(HasStartupStatusProperty, value); }
    public bool HasStartupWarning { get => GetValue(HasStartupWarningProperty); set => SetValue(HasStartupWarningProperty, value); }
    public string? StartupActionLabel { get => GetValue(StartupActionLabelProperty); set => SetValue(StartupActionLabelProperty, value); }
    public ICommand? StartupActionCommand { get => GetValue(StartupActionCommandProperty); set => SetValue(StartupActionCommandProperty, value); }
    public string? Activity { get => GetValue(ActivityProperty); set => SetValue(ActivityProperty, value); }
    public bool IsRunning { get => GetValue(IsRunningProperty); set => SetValue(IsRunningProperty, value); }
    public bool HasConversation => _hasConversation;
    public bool HasStatus => _hasStatus;
    public bool HasExpansion => _hasExpansion;
    public string MoreLabel => ConversationHeaderText.MoreLabel;
    public string ContextLabel => ConversationHeaderText.ContextLabel;
    public string ExpandLabel => InspectorText.PreviewExpand;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (ChatMoreButton is null) return;
        if (change.Property == ConversationTargetProperty || change.Property == MenuContentProperty
            || change.Property == DataContextProperty || change.Property == ToggleContextCommandProperty
            || change.Property == ToggleInspectorCommandProperty || change.Property == ExpandInspectorCommandProperty
            || change.Property == IsInspectorOpenProperty)
            ChatMoreButton.Flyout?.Hide();
        if (change.Property == ConversationTargetProperty || change.Property == MenuContentProperty
            || change.Property == ToggleContextCommandProperty || change.Property == ToggleInspectorCommandProperty
            || change.Property == ExpandInspectorCommandProperty || change.Property == StartupActionCommandProperty
            || change.Property == HasStartupStatusProperty || change.Property == HasStartupWarningProperty
            || change.Property == IsRunningProperty || change.Property == IsInspectorOpenProperty)
            UpdateState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        ChatMoreButton.Flyout?.Hide();
        base.OnDetachedFromVisualTree(args);
    }

    private void UpdateState()
    {
        SetAndRaise(HasConversationProperty, ref _hasConversation, ConversationTarget is not null);
        SetAndRaise(HasStatusProperty, ref _hasStatus, HasStartupStatus || HasStartupWarning || IsRunning);
        SetAndRaise(HasExpansionProperty, ref _hasExpansion, HasConversation && !IsInspectorOpen && ExpandInspectorCommand is not null);
        ChatMoreButton.IsEnabled = HasConversation && (MenuContent is not null || HasExpansion);
        ChatContextButton.IsEnabled = HasConversation && ToggleContextCommand is not null;
        ChatInspectorButton.IsEnabled = HasConversation && ToggleInspectorCommand is not null;
        ChatExpandInspectorButton.IsEnabled = HasExpansion;
        StartupSettingsButton.IsEnabled = StartupActionCommand is not null;
        StatusLayout.ColumnDefinitions[0].Width = HasStartupStatus ? GridLength.Star : new GridLength(0);
        StatusLayout.ColumnDefinitions[2].Width = IsRunning ? GridLength.Star : new GridLength(0);
    }
}
