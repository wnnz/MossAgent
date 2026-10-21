using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.App.Tests;

internal sealed class TrackedAtomicWriteSessions(bool deny) : IAtomicFileWriteSessionFactory
{
    public int Creates { get; private set; }
    public int ArtifactCreates { get; private set; }

    public IAtomicFileWriteSession Create(string path, IReadOnlyList<string> roots, CancellationToken token)
    {
        Creates++;
        if (System.IO.Path.GetFileName(path).StartsWith("edit-", StringComparison.Ordinal)
            && path.EndsWith(".snapshot", StringComparison.Ordinal)) ArtifactCreates++;
        if (deny) throw new UnauthorizedAccessException("synthetic write session refusal");
        return new WindowsAtomicFileWriteSessionFactory().Create(path, roots, token);
    }
}
