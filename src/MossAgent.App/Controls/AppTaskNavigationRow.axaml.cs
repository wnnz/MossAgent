using System.Windows.Input;
using Avalonia;
using Avalonia.VisualTree;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;

namespace MossAgent.App.Controls;

public sealed partial class AppTaskNavigationRow : AppUserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<AppTaskNavigationRow, string>(nameof(Title), string.Empty);
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<AppTaskNavigationRow, bool>(nameof(IsSelected));
    public static readonly StyledProperty<bool> IsProjectTaskProperty =
        AvaloniaProperty.Register<AppTaskNavigationRow, bool>(nameof(IsProjectTask));
    public static readonly StyledProperty<bool> IsRunningProperty =
        AvaloniaProperty.Register<AppTaskNavigationRow, bool>(nameof(IsRunning));
    public static readonly StyledProperty<bool> IsOptionsOpenProperty =
        AvaloniaProperty.Register<AppTaskNavigationRow, bool>(nameof(IsOptionsOpen));
    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<AppTaskNavigationRow, object?>(nameof(CommandParameter));
    public static readonly StyledProperty<ICommand?> SelectCommandProperty =
        AvaloniaProperty.Register<AppTaskNavigationRow, ICommand?>(nameof(SelectCommand));
    public static readonly StyledProperty<ICommand?> RenameCommandProperty =
        AvaloniaProperty.Register<AppTaskNavigationRow, ICommand?>(nameof(RenameCommand));
    public static readonly StyledProperty<ICommand?> ArchiveCommandProperty =
        AvaloniaProperty.Register<AppTaskNavigationRow, ICommand?>(nameof(ArchiveCommand));
    public static readonly StyledProperty<ICommand?> TogglePinCommandProperty =
        AvaloniaProperty.Register<AppTaskNavigationRow, ICommand?>(nameof(TogglePinCommand));
    public static readonly StyledProperty<string> PinLabelProperty =
        AvaloniaProperty.Register<AppTaskNavigationRow, string>(nameof(PinLabel), SettingsText.Get("PinTask"));

    private readonly AppFlyout _optionsFlyout;
    private readonly List<Visual> _ancestors = [];

    public AppTaskNavigationRow()
    {
        InitializeComponent();
        _optionsFlyout = (AppFlyout)TaskOptionsButton.Flyout!;
    }

    public bool IsRunning { get => GetValue(IsRunningProperty); set => SetValue(IsRunningProperty, value); }
    public bool IsOptionsOpen { get => GetValue(IsOptionsOpenProperty); set => SetValue(IsOptionsOpenProperty, value); }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public bool IsProjectTask
    {
        get => GetValue(IsProjectTaskProperty);
        set => SetValue(IsProjectTaskProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public ICommand? SelectCommand
    {
        get => GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }

    public ICommand? RenameCommand
    {
        get => GetValue(RenameCommandProperty);
        set => SetValue(RenameCommandProperty, value);
    }

    public ICommand? ArchiveCommand
    {
        get => GetValue(ArchiveCommandProperty);
        set => SetValue(ArchiveCommandProperty, value);
    }

    public ICommand? TogglePinCommand
    {
        get => GetValue(TogglePinCommandProperty);
        set => SetValue(TogglePinCommandProperty, value);
    }

    public string PinLabel
    {
        get => GetValue(PinLabelProperty);
        set => SetValue(PinLabelProperty, value);
    }

    public string OptionsLabel => SettingsText.Get("TaskOptions");
    public string RenameLabel => SettingsText.Get("RenameTask");
    public string ArchiveLabel => SettingsText.Get("ArchiveTask");
    public string RunningLabel => WorkspaceText.Get("ToolRunning");

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CommandParameterProperty || change.Property == DataContextProperty
            || change.Property == RenameCommandProperty || change.Property == ArchiveCommandProperty
            || change.Property == TogglePinCommandProperty
            || (change.Property == IsVisibleProperty && !IsVisible)
            || (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled))
            CloseOptions();
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
        foreach (var ancestor in _ancestors) ancestor.PropertyChanged -= HandleAncestorChanged;
        _ancestors.Clear();
        CloseOptions();
        base.OnDetachedFromVisualTree(args);
    }

    private void HandleAncestorChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property == IsVisibleProperty && !IsEffectivelyVisible) CloseOptions();
    }

    private void CloseOptions()
    {
        _optionsFlyout?.Hide();
        SetCurrentValue(IsOptionsOpenProperty, false);
    }
}
