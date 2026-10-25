namespace MossAgent.Application.Persistence;

public static class GitCommitDraftValidation
{
    public static void Validate(GitCommitDraftUpdate update)
    {
        if (update.TaskId == Guid.Empty
            || update.Text is null
            || update.Text.Length > GitCommitDraft.MaximumTextLength
            || update.ExpectedRevision < 0
            || update.Text.Any(character => char.IsControl(character) && character is not ('\r' or '\n' or '\t')))
            throw new ArgumentException("Invalid git commit draft update.");
    }

    public static void ValidateTaskId(Guid taskId)
    {
        if (taskId == Guid.Empty) throw new ArgumentException("Invalid git commit draft task.");
    }

    public static void ValidateDelete(Guid taskId, long expectedRevision)
    {
        if (taskId == Guid.Empty || expectedRevision < 0)
            throw new ArgumentException("Invalid git commit draft deletion.");
    }
}
