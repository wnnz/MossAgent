using MossAgent.Domain;

namespace MossAgent.Tools.Abstractions.Files;

public interface IRootIdentitySession : IDisposable
{
    IReadOnlyList<DirectoryObjectIdentity> Roots { get; }
}
