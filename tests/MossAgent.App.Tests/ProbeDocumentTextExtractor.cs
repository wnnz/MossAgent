using MossAgent.Application.Attachments;

namespace MossAgent.App.Tests;

internal sealed class ProbeDocumentTextExtractor : IAttachmentDocumentTextExtractor
{
    public Func<ReadOnlyMemory<byte>, CancellationToken, Task<DocumentTextExtraction>> Extract { get; set; } =
        (_, _) => Task.FromResult(new DocumentTextExtraction("extracted pdf text", 1, 1, false));
    public int Calls { get; private set; }

    public Task<DocumentTextExtraction> ExtractAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        Calls++;
        return Extract(content, cancellationToken);
    }
}
