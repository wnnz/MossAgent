using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class ArtifactWriteFixture : IDisposable
{
    private readonly ReadHandleDirectoryFixture _directory = new();
    public string Root => _directory.Root;
    public string Source { get; }
    public string ArtifactParent { get; }
    public string ArtifactRoot => Context.ArtifactDirectory!;
    public ToolExecutionContext Context { get; }
    public CallbackAtomicWriteSessions Sessions { get; }
    public FilePreviewTool Tool { get; }
    public CancellationToken Token => Xunit.TestContext.Current.CancellationToken;

    public ArtifactWriteFixture(CallbackAtomicWriteSessions? sessions = null, IAuthorizedDirectoryCreator? directories = null)
    {
        var sourceRoot = _directory.DirectoryFor("project");
        Source = _directory.FileFor(sourceRoot);
        ArtifactParent = _directory.DirectoryFor("artifacts");
        var task = Guid.NewGuid();
        Context = new(task, sourceRoot, [sourceRoot], ApprovalPolicy.ReadOnly,
            ArtifactDirectory: Path.Combine(ArtifactParent, task.ToString("N")));
        Sessions = sessions ?? new();
        Tool = new(new(), artifactSessions: new WindowsArtifactFileSessionFactory(Sessions,
            directories ?? new WindowsAuthorizedDirectoryCreator()));
    }

    public Task<ToolResult> PreviewAsync(ToolExecutionContext? context = null, CancellationToken? token = null, TimeProvider? time = null) =>
        new ToolExecutor(new([Tool]), new PolicyApprovalService((_, _, _) => throw new InvalidOperationException("Read preview must not request approval.")),
            readHandleValidator: new WindowsFileReadHandleValidator(), timeProvider: time)
            .ExecuteAsync(new(Guid.NewGuid().ToString("N"), "filesystem.preview_file", JsonSerializer.SerializeToElement(new { path = Source })),
                context ?? Context, token ?? Token);

    public void Dispose() => _directory.Dispose();
}
