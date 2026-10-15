using Avalonia;
using Avalonia.Markup.Xaml;

namespace MossAgent.App.Controls;

public sealed partial class AppDiffStatistics : AppUserControl
{
    public static readonly StyledProperty<string?> AddedLabelProperty =
        AvaloniaProperty.Register<AppDiffStatistics, string?>(nameof(AddedLabel));
    public static readonly StyledProperty<string?> RemovedLabelProperty =
        AvaloniaProperty.Register<AppDiffStatistics, string?>(nameof(RemovedLabel));

    public AppDiffStatistics()
    {
        Classes.Add("app-diff-statistics");
        AvaloniaXamlLoader.Load(this);
    }

    public string? AddedLabel
    {
        get => GetValue(AddedLabelProperty);
        set => SetValue(AddedLabelProperty, value);
    }

    public string? RemovedLabel
    {
        get => GetValue(RemovedLabelProperty);
        set => SetValue(RemovedLabelProperty, value);
    }
}
