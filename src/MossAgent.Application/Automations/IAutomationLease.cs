namespace MossAgent.Application.Automations;

public interface IAutomationLease
{
    bool TryAcquire();
    void Release();
}
