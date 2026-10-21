namespace MossAgent.Application.Authorization;

public interface IProjectAuthorizationChanges
{
    event Action<Guid, long>? Changed;
    void Publish(Guid projectId, long revision);
}
