namespace MossAgent.Domain;

public sealed record ConversationDraftScope(Guid? ProjectId, Guid? TaskId)
{
    public string Key => TaskId is { } taskId ? $"task:{taskId:D}"
        : ProjectId is { } projectId ? $"project:{projectId:D}" : "projectless";

    public void Validate()
    {
        if (ProjectId == Guid.Empty || TaskId == Guid.Empty)
            throw new ArgumentException("Invalid conversation draft scope.");
    }
}
