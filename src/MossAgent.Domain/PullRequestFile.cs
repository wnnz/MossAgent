namespace MossAgent.Domain;

public sealed record PullRequestFile(string Path, string? PreviousPath, string Status, int Additions,
    int Deletions, string Patch, PullRequestPatchState PatchState);
