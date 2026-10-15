using System.Windows.Input;
using Avalonia.Data.Converters;
using Avalonia.Data;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace MossAgent.App.Controls;

public sealed class AppProjectPrompt : ContentControl
{
    public static readonly StyledProperty<string?> PrefixProperty =
        AvaloniaProperty.Register<AppProjectPrompt, string?>(nameof(Prefix));
    public static readonly StyledProperty<string?> SuffixProperty =
        AvaloniaProperty.Register<AppProjectPrompt, string?>(nameof(Suffix));
    public static readonly StyledProperty<string?> ProjectNameProperty =
        AvaloniaProperty.Register<AppProjectPrompt, string?>(nameof(ProjectName));
    public static readonly StyledProperty<string?> SwitchLabelProperty =
        AvaloniaProperty.Register<AppProjectPrompt, string?>(nameof(SwitchLabel));
    public static readonly StyledProperty<ICommand?> SelectProjectCommandProperty =
        AvaloniaProperty.Register<AppProjectPrompt, ICommand?>(nameof(SelectProjectCommand));
    public static readonly StyledProperty<AppFlyout?> ProjectFlyoutProperty =
        AvaloniaProperty.Register<AppProjectPrompt, AppFlyout?>(nameof(ProjectFlyout));

    public AppProjectPrompt()
    {
        Classes.Add("app-project-prompt");
        var prefix = new AppText { Classes = { "project-prompt-fragment" } };
        prefix.Bind(TextBlock.TextProperty, new Binding(nameof(Prefix)) { Source = this });
        var suffix = new AppText { Classes = { "project-prompt-fragment" } };
        suffix.Bind(TextBlock.TextProperty, new Binding(nameof(Suffix)) { Source = this });
        var name = new AppText { Classes = { "empty-project-name" } };
        name.Bind(TextBlock.TextProperty, new Binding(nameof(ProjectName)) { Source = this });
        var project = new AppButton { Classes = { "ghost", "empty-project-link" }, Content = name };
        project.Bind(Button.CommandProperty, new Binding(nameof(SelectProjectCommand)) { Source = this });
        project.Bind(IsEnabledProperty, new Binding(nameof(SelectProjectCommand)) { Source = this, Converter = ObjectConverters.IsNotNull });
        project.Bind(Button.FlyoutProperty, new Binding(nameof(ProjectFlyout)) { Source = this });
        project.Bind(ToolTip.TipProperty, new Binding(nameof(SwitchLabel)) { Source = this });
        project.Bind(AutomationProperties.NameProperty, new Binding(nameof(ProjectName)) { Source = this });
        Content = new AppProjectPromptLayout(prefix, project, suffix);
    }

    public string? Prefix { get => GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }
    public string? Suffix { get => GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }
    public string? ProjectName { get => GetValue(ProjectNameProperty); set => SetValue(ProjectNameProperty, value); }
    public string? SwitchLabel { get => GetValue(SwitchLabelProperty); set => SetValue(SwitchLabelProperty, value); }
    public ICommand? SelectProjectCommand { get => GetValue(SelectProjectCommandProperty); set => SetValue(SelectProjectCommandProperty, value); }
    public AppFlyout? ProjectFlyout { get => GetValue(ProjectFlyoutProperty); set => SetValue(ProjectFlyoutProperty, value); }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        ProjectFlyout?.Hide();
        base.OnDetachedFromVisualTree(args);
    }

    protected override Type StyleKeyOverride => typeof(ContentControl);

}
