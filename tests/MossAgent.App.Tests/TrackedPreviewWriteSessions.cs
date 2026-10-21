using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.App.Tests;

internal sealed class TrackedPreviewWriteSessions : IAtomicFileWriteSessionFactory
{
    public bool Refuse { get; set; }
    public int PreviewCreates { get; private set; }
    public IAtomicFileWriteSession Create(string path, IReadOnlyList<string> roots, CancellationToken token)
    {
        if (Path.GetFileName(path).StartsWith("preview-", StringComparison.Ordinal))
        {
            PreviewCreates++;
            if (Refuse) throw new FileWriteSessionException("synthetic safe preview refusal", "artifact_write_unsafe");
        }
        return new WindowsAtomicFileWriteSessionFactory().Create(path, roots, token);
    }
}
