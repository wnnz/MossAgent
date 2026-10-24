using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Infrastructure.Tests;

internal sealed class StubWorktreeClaim(string path) : IWorktreeTargetClaim
{
    public string Path { get; } = path;
    public DirectoryObjectIdentity Identity { get; } =
        new(path, "VOLUME{STUB}", new string('1', 32));
    public void Verify() { }
    public void Dispose() { }
}
