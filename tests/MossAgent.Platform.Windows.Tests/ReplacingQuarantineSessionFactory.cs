using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class ReplacingQuarantineSessionFactory(
    IPathQuarantineSessionFactory inner,
    Action afterMove,
    Exception? deleteFailure = null) : IPathQuarantineSessionFactory
{
    public IPathQuarantineSession Create(string source, string destination,
        IReadOnlyList<string> authorizedRoots, CancellationToken cancellationToken) =>
        new ReplacingQuarantineSession(
            inner.Create(source, destination, authorizedRoots, cancellationToken),
            afterMove, deleteFailure);
}
