namespace MossAgent.Domain;

public enum PullRequestPatchState
{
    Available,
    Missing,
    Incomplete,
    TooLarge
}
