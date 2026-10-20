namespace MossAgent.Domain;

public static class PullRequestCommentLocator
{
    public static PullRequestCommentLocationState Locate(PullRequestRevision revision, PullRequestFile? file, PullRequestComment comment)
    {
        revision.Validate();
        if (comment.Kind != "inline" || !string.Equals(comment.CommitRevision, revision.HeadRevision, StringComparison.OrdinalIgnoreCase))
            return PullRequestCommentLocationState.Historical;
        if (comment.Line is not > 0 || comment.Side is not ("LEFT" or "RIGHT")) return PullRequestCommentLocationState.MissingLine;
        if (file is null || !string.Equals(file.Path, comment.Path, StringComparison.Ordinal)) return PullRequestCommentLocationState.FileNotLoaded;
        if (file.PatchState != PullRequestPatchState.Available) return PullRequestCommentLocationState.PatchUnavailable;
        var map = PullRequestPatchMap.Parse(file.Patch, file.Additions, file.Deletions);
        if (map is null) return PullRequestCommentLocationState.PatchUnavailable;
        if (!map.Contains(comment.Side, comment.Line.Value)) return PullRequestCommentLocationState.InvalidLine;
        if (comment.StartLine is not null || comment.StartSide is not null)
        {
            if (comment.StartLine is null || comment.StartSide != comment.Side) return PullRequestCommentLocationState.UnsupportedRange;
            if (!map.ContainsRange(comment.Side, comment.StartLine.Value, comment.Line.Value)) return PullRequestCommentLocationState.InvalidLine;
        }
        return PullRequestCommentLocationState.Current;
    }
}
