namespace MossAgent.Tools.Abstractions.Files;

public interface IWorktreeRemovalGuard : IDisposable
{
    void ConfirmRemoved();
}
