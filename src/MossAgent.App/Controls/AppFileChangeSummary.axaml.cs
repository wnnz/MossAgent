using Avalonia;
using Avalonia.Markup.Xaml;
using MossAgent.App.ViewModels;

namespace MossAgent.App.Controls;

public sealed partial class AppFileChangeSummary : AppUserControl
{
    public static readonly StyledProperty<WorkspaceFileChangesViewModel?> SummaryProperty =
        AvaloniaProperty.Register<AppFileChangeSummary, WorkspaceFileChangesViewModel?>(nameof(Summary));

    public AppFileChangeSummary()
    {
        Classes.Add("app-file-change-summary");
        AvaloniaXamlLoader.Load(this);
    }

    public WorkspaceFileChangesViewModel? Summary
    {
        get => GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }
}
