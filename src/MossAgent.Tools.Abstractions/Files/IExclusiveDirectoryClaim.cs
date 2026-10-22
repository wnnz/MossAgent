using MossAgent.Domain;

namespace MossAgent.Tools.Abstractions.Files;

public interface IExclusiveDirectoryClaim : IDisposable
{
    string Path { get; }
    DirectoryObjectIdentity Identity { get; }
    void Validate();
}
