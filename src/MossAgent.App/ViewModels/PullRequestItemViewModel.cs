using CommunityToolkit.Mvvm.ComponentModel;
using MossAgent.App.Resources;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class PullRequestItemViewModel : ObservableObject
{
    private PullRequestViewedState _viewedState;

    public PullRequestItemViewModel(PullRequestSummary summary, string? markedHeadRevision = null)
    {
        Summary = summary;
        ApplyMark(markedHeadRevision);
    }

    public PullRequestSummary Summary { get; }
    public string MarkLabel => ViewedState switch
    {
        PullRequestViewedState.Viewed => PullRequestText.MarkViewed,
        PullRequestViewedState.UpdatedSinceViewed => PullRequestText.MarkUpdated,
        _ => PullRequestText.MarkUnviewed,
    };

    public PullRequestViewedState ViewedState
    {
        get => _viewedState;
        private set { if (SetProperty(ref _viewedState, value)) OnPropertyChanged(nameof(MarkLabel)); }
    }

    // Applies the persisted mark head: none means unviewed, a head equal to the listed
    // revision means viewed, and any other stored head means the PR moved since viewing.
    public void ApplyMark(string? markedHeadRevision) =>
        ViewedState = markedHeadRevision is null ? PullRequestViewedState.Unviewed
            : Summary.HeadRevision.Equals(markedHeadRevision, StringComparison.OrdinalIgnoreCase)
                ? PullRequestViewedState.Viewed : PullRequestViewedState.UpdatedSinceViewed;
}
