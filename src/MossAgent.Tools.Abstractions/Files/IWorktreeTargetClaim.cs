using MossAgent.Domain;

namespace MossAgent.Tools.Abstractions.Files;

public interface IWorktreeTargetClaim : IDisposable
{
    string Path { get; }
    DirectoryObjectIdentity Identity { get; }
    void Verify();
}
