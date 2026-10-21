namespace MossAgent.Application.Authorization;

public sealed class ProjectAuthorizationChanges : IProjectAuthorizationChanges
{
    public event Action<Guid, long>? Changed;

    public void Publish(Guid projectId, long revision)
    {
        foreach (var handler in Changed?.GetInvocationList().Cast<Action<Guid, long>>() ?? [])
        {
            try { handler(projectId, revision); }
            catch (Exception) { }
        }
    }
}
