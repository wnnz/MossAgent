using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceFileChangeViewModel(ToolFileChange change, string displayPath) : ObservableObject
{
    public ToolFileChange Change { get; } = change;
    public string DisplayPath { get; } = displayPath;
    public string Path => Change.Path;
    public ToolFileChangeStatistics? Statistics { get; private set; }
    public bool IsStatisticsResolved { get; private set; }
    public bool HasStatistics => Statistics is not null;
    public bool IsStatisticsUnavailable => IsStatisticsResolved && !HasStatistics;
    public string AddedLabel => $"+{Statistics?.Added.ToString(CultureInfo.CurrentCulture)}";
    public string RemovedLabel => $"-{Statistics?.Removed.ToString(CultureInfo.CurrentCulture)}";
    public string UnavailableLabel => WorkspaceText.Get("FileStatisticsUnavailableLabel");

    internal void SetStatistics(ToolFileChangeStatistics? statistics, bool resolved)
    {
        if (Statistics == statistics && IsStatisticsResolved == resolved) return;
        Statistics = statistics;
        IsStatisticsResolved = resolved;
        OnPropertyChanged(nameof(HasStatistics));
        OnPropertyChanged(nameof(IsStatisticsUnavailable));
        OnPropertyChanged(nameof(AddedLabel));
        OnPropertyChanged(nameof(RemovedLabel));
    }
}
