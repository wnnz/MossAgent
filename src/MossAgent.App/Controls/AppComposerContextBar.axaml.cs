using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using MossAgent.App.Resources;
using MossAgent.Domain;

namespace MossAgent.App.Controls;

public sealed partial class AppComposerContextBar : AppUserControl
{
    public static readonly StyledProperty<ProjectProfile?> ProjectProperty =
        AvaloniaProperty.Register<AppComposerContextBar, ProjectProfile?>(nameof(Project));
    public static readonly DirectProperty<AppComposerContextBar, string> ProjectLabelProperty =
        AvaloniaProperty.RegisterDirect<AppComposerContextBar, string>(nameof(ProjectLabel), bar => bar.ProjectLabel);
    public static readonly StyledProperty<string?> BranchLabelProperty =
        AvaloniaProperty.Register<AppComposerContextBar, string?>(nameof(BranchLabel));
    public static readonly StyledProperty<string?> AgentNameProperty =
        AvaloniaProperty.Register<AppComposerContextBar, string?>(nameof(AgentName));
    public static readonly StyledProperty<bool> HasAgentsProperty =
        AvaloniaProperty.Register<AppComposerContextBar, bool>(nameof(HasAgents));
    public static readonly StyledProperty<bool> CanChooseAgentProperty =
        AvaloniaProperty.Register<AppComposerContextBar, bool>(nameof(CanChooseAgent), true);
    public static readonly StyledProperty<IEnumerable<AgentProfile>?> AgentsProperty =
        AvaloniaProperty.Register<AppComposerContextBar, IEnumerable<AgentProfile>?>(nameof(Agents));
    public static readonly StyledProperty<ICommand?> OpenProjectCommandProperty =
        AvaloniaProperty.Register<AppComposerContextBar, ICommand?>(nameof(OpenProjectCommand));
    public static readonly StyledProperty<ICommand?> SelectAgentCommandProperty =
        AvaloniaProperty.Register<AppComposerContextBar, ICommand?>(nameof(SelectAgentCommand));
    public static readonly StyledProperty<Control?> ProjectPickerContentProperty =
        AvaloniaProperty.Register<AppComposerContextBar, Control?>(nameof(ProjectPickerContent));
    public static readonly StyledProperty<Control?> EnvironmentPickerContentProperty =
        AvaloniaProperty.Register<AppComposerContextBar, Control?>(nameof(EnvironmentPickerContent));
    public static readonly DirectProperty<AppComposerContextBar, bool> IsProjectAvailableProperty =
        AvaloniaProperty.RegisterDirect<AppComposerContextBar, bool>(nameof(IsProjectAvailable), bar => bar.IsProjectAvailable);
    public static readonly DirectProperty<AppComposerContextBar, bool> IsAgentAvailableProperty =
        AvaloniaProperty.RegisterDirect<AppComposerContextBar, bool>(nameof(IsAgentAvailable), bar => bar.IsAgentAvailable);

    private bool _isProjectAvailable;
    private bool _isAgentAvailable;
    private bool _isCondensed;
    private bool _isStacked;

    public AppComposerContextBar()
    {
        Classes.Add("app-composer-context-bar");
        InitializeComponent();
    }

    public ProjectProfile? Project { get => GetValue(ProjectProperty); set => SetValue(ProjectProperty, value); }
    public string ProjectLabel => Project?.Name ?? ProjectlessChatText.Label;
    public string? BranchLabel { get => GetValue(BranchLabelProperty); set => SetValue(BranchLabelProperty, value); }
    public string? AgentName { get => GetValue(AgentNameProperty); set => SetValue(AgentNameProperty, value); }
    public bool HasAgents { get => GetValue(HasAgentsProperty); set => SetValue(HasAgentsProperty, value); }
    public bool CanChooseAgent { get => GetValue(CanChooseAgentProperty); set => SetValue(CanChooseAgentProperty, value); }
    public IEnumerable<AgentProfile>? Agents { get => GetValue(AgentsProperty); set => SetValue(AgentsProperty, value); }
    public ICommand? OpenProjectCommand { get => GetValue(OpenProjectCommandProperty); set => SetValue(OpenProjectCommandProperty, value); }
    public ICommand? SelectAgentCommand { get => GetValue(SelectAgentCommandProperty); set => SetValue(SelectAgentCommandProperty, value); }
    public Control? ProjectPickerContent { get => GetValue(ProjectPickerContentProperty); set => SetValue(ProjectPickerContentProperty, value); }
    public Control? EnvironmentPickerContent { get => GetValue(EnvironmentPickerContentProperty); set => SetValue(EnvironmentPickerContentProperty, value); }
    public bool IsProjectAvailable => _isProjectAvailable;
    public bool IsAgentAvailable => _isAgentAvailable;
    public string DefaultAgentLabel => SettingsText.Get("ComposerDefaultAgent");

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = Bounds.Width > 0 ? Math.Min(Bounds.Width, availableSize.Width) : availableSize.Width;
        var condensed = HasAgents && width < 440;
        var stacked = Project is not null && EnvironmentPickerContent is not null && width < 560;
        if (_isCondensed != condensed || _isStacked != stacked)
        {
            _isCondensed = condensed;
            _isStacked = stacked;
            Classes.Set("stacked", stacked);
            ContextLayout.ColumnDefinitions[0].Width = condensed || stacked ? GridLength.Star : GridLength.Auto;
            ContextLayout.ColumnDefinitions[1].Width = stacked ? new GridLength(0) : GridLength.Star;
            ContextLayout.ColumnDefinitions[2].Width = condensed || stacked ? GridLength.Star : GridLength.Auto;
            Grid.SetColumn(ContextDetails, stacked ? 0 : 1);
            Grid.SetColumnSpan(ContextDetails, stacked ? 3 : 1);
            Grid.SetRow(ContextDetails, stacked ? 1 : 0);
        }
        Grid.SetColumnSpan(ProjectButton, stacked ? HasAgents ? 2 : 3 : 1);
        return base.MeasureOverride(availableSize);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty && change.GetOldValue<Rect>().Width != Bounds.Width)
            InvalidateMeasure();
        if (change.Property == ProjectProperty || change.Property == ProjectPickerContentProperty
            || change.Property == OpenProjectCommandProperty)
        {
            SetAndRaise(IsProjectAvailableProperty, ref _isProjectAvailable,
                ProjectPickerContent is not null && OpenProjectCommand is not null);
            if (change.Property == ProjectProperty)
                RaisePropertyChanged(ProjectLabelProperty,
                    change.GetOldValue<ProjectProfile?>()?.Name ?? ProjectlessChatText.Label, ProjectLabel);
            CloseFlyouts();
            InvalidateMeasure();
        }
        if (change.Property == HasAgentsProperty || change.Property == SelectAgentCommandProperty || change.Property == CanChooseAgentProperty)
        {
            SetAndRaise(IsAgentAvailableProperty, ref _isAgentAvailable, HasAgents && CanChooseAgent && SelectAgentCommand is not null);
            InvalidateMeasure();
            if (!IsAgentAvailable) CloseFlyouts();
        }
        if (change.Property == AgentNameProperty || change.Property == AgentsProperty) CloseFlyouts();
        if (change.Property == EnvironmentPickerContentProperty) InvalidateMeasure();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        CloseFlyouts();
        base.OnDetachedFromVisualTree(args);
    }

    private void CloseFlyouts()
    {
        ProjectButton?.Flyout?.Hide();
        AgentButton?.Flyout?.Hide();
    }
}
