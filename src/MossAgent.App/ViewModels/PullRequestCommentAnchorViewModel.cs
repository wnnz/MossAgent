using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using MossAgent.App.Resources;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class PullRequestCommentAnchorViewModel
{
    public PullRequestCommentAnchorViewModel(PullRequestComment comment, PullRequestCommentLocationState state,
        Action<PullRequestComment> locate, Func<bool> isCurrent)
    {
        Comment = comment; State = state;
        LocateCommand = new RelayCommand(() => locate(comment), () => State == PullRequestCommentLocationState.Current && isCurrent());
    }
    public PullRequestComment Comment { get; }
    public PullRequestCommentLocationState State { get; }
    public string LocationHint => PullRequestText.Get("Location_" + State);
    public string LocationLabel => Comment.Line is null ? PullRequestText.Get("CommentNoLine")
        : Comment.StartLine is null ? string.Format(CultureInfo.CurrentCulture, PullRequestText.Get("CommentLineFormat"), Comment.Line, Comment.Side)
        : string.Format(CultureInfo.CurrentCulture, PullRequestText.Get("CommentRangeFormat"), Comment.StartSide, Comment.StartLine, Comment.Side, Comment.Line);
    public IRelayCommand LocateCommand { get; }
}
