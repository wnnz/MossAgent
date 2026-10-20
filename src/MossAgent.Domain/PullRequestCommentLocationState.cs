namespace MossAgent.Domain;

public enum PullRequestCommentLocationState
{
    Current,
    Historical,
    FileNotLoaded,
    PatchUnavailable,
    MissingLine,
    InvalidLine,
    UnsupportedRange
}
