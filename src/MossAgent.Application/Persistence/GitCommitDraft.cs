namespace MossAgent.Application.Persistence;

public sealed record GitCommitDraft(string Text, long Revision)
{
    // Matches the git.commit message limit in the tool schema.
    public const int MaximumTextLength = 8192;
}
