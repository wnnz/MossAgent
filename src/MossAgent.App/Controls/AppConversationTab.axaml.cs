using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Resources;

namespace MossAgent.App.Controls;

public sealed partial class AppConversationTab : AppUserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<AppConversationTab, string>(nameof(Title), string.Empty);
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<AppConversationTab, bool>(nameof(IsSelected));
    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<AppConversationTab, object?>(nameof(CommandParameter));
    public static readonly StyledProperty<ICommand?> SelectCommandProperty =
        AvaloniaProperty.Register<AppConversationTab, ICommand?>(nameof(SelectCommand));
    public static readonly StyledProperty<ICommand?> CloseCommandProperty =
        AvaloniaProperty.Register<AppConversationTab, ICommand?>(nameof(CloseCommand));
    public static readonly StyledProperty<string> OptionsLabelProperty =
        AvaloniaProperty.Register<AppConversationTab, string>(nameof(OptionsLabel), ConversationTabText.Options);
    public static readonly StyledProperty<string> CloseLabelProperty =
        AvaloniaProperty.Register<AppConversationTab, string>(nameof(CloseLabel), ConversationTabText.Close);

    private readonly List<Visual> _ancestors = [];
    private ICommand? _observedSelect;
    private ICommand? _observedClose;
    private bool _attached;
    private int _menuVersion;

    public AppConversationTab()
    {
        Classes.Add("app-conversation-tab");
        InitializeComponent();
        CloseButton.Click += HandleCloseClick;
        OptionsButton.Flyout!.Opened += HandleMenuOpened;
        OptionsButton.Flyout.Closed += (_, _) => _menuVersion++;
        UpdateAvailability();
    }

    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
    public ICommand? SelectCommand { get => GetValue(SelectCommandProperty); set => SetValue(SelectCommandProperty, value); }
    public ICommand? CloseCommand { get => GetValue(CloseCommandProperty); set => SetValue(CloseCommandProperty, value); }
    public string OptionsLabel { get => GetValue(OptionsLabelProperty); set => SetValue(OptionsLabelProperty, value); }
    public string CloseLabel { get => GetValue(CloseLabelProperty); set => SetValue(CloseLabelProperty, value); }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs args)
    {
        base.OnAttachedToVisualTree(args);
        _attached = true;
        foreach (var ancestor in this.GetVisualAncestors())
        {
            _ancestors.Add(ancestor);
            ancestor.PropertyChanged += HandleAncestorChanged;
        }
        ObserveCommands();
        UpdateAvailability();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        _attached = false;
        foreach (var ancestor in _ancestors) ancestor.PropertyChanged -= HandleAncestorChanged;
        _ancestors.Clear();
        ObserveCommands();
        UpdateAvailability();
        base.OnDetachedFromVisualTree(args);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (OptionsButton is null) return;
        if (change.Property == CommandParameterProperty || change.Property == SelectCommandProperty
            || change.Property == CloseCommandProperty || change.Property == DataContextProperty)
        {
            OptionsButton.Flyout?.Hide();
            ObserveCommands();
            UpdateAvailability();
        }
        else if (change.Property == IsVisibleProperty || change.Property == IsEnabledProperty) UpdateAvailability();
    }

    private void ObserveCommands()
    {
        if (_observedSelect is not null) _observedSelect.CanExecuteChanged -= HandleCanExecuteChanged;
        if (_observedClose is not null) _observedClose.CanExecuteChanged -= HandleCanExecuteChanged;
        _observedSelect = _attached ? SelectCommand : null;
        _observedClose = _attached ? CloseCommand : null;
        if (_observedSelect is not null) _observedSelect.CanExecuteChanged += HandleCanExecuteChanged;
        if (_observedClose is not null) _observedClose.CanExecuteChanged += HandleCanExecuteChanged;
    }

    private void HandleCanExecuteChanged(object? sender, EventArgs args) => UpdateAvailability();
    private void HandleMenuOpened(object? sender, EventArgs args)
    {
        _menuVersion++;
        CloseButton.Focus();
    }
    private void HandleCloseClick(object? sender, RoutedEventArgs args)
    {
        var version = _menuVersion;
        Dispatcher.UIThread.Post(() =>
        {
            if (version == _menuVersion) OptionsButton.Flyout?.Hide();
        }, DispatcherPriority.Background);
    }
    private void HandleAncestorChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property == IsVisibleProperty || args.Property == IsEnabledProperty) UpdateAvailability();
    }

    private void UpdateAvailability()
    {
        var active = _attached && IsVisible && IsEnabled && IsEffectivelyVisible && IsEffectivelyEnabled && CommandParameter is not null;
        SelectButton.IsEnabled = active && SelectCommand?.CanExecute(CommandParameter) == true;
        CloseButton.IsEnabled = active && CloseCommand?.CanExecute(CommandParameter) == true;
        OptionsButton.IsEnabled = active && CloseCommand?.CanExecute(CommandParameter) == true;
        if (!OptionsButton.IsEnabled) OptionsButton.Flyout?.Hide();
    }
}
