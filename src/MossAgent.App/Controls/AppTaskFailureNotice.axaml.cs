using Avalonia;
using Avalonia.Markup.Xaml;
using MossAgent.App.ViewModels;

namespace MossAgent.App.Controls;

public sealed partial class AppTaskFailureNotice : AppUserControl
{
    public static readonly StyledProperty<string> HintProperty =
        AvaloniaProperty.Register<AppTaskFailureNotice, string>(nameof(Hint), string.Empty);

    public static readonly StyledProperty<WorkspaceRunRetryViewModel?> RetryProperty =
        AvaloniaProperty.Register<AppTaskFailureNotice, WorkspaceRunRetryViewModel?>(nameof(Retry));

    public AppTaskFailureNotice() => AvaloniaXamlLoader.Load(this);

    public string Hint
    {
        get => GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    public WorkspaceRunRetryViewModel? Retry
    {
        get => GetValue(RetryProperty);
        set => SetValue(RetryProperty, value);
    }
}
