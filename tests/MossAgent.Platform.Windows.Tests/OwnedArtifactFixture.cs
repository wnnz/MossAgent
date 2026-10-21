using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class OwnedArtifactFixture : IDisposable
{
    private readonly ReadHandleDirectoryFixture _directory = new();
    public string Root => _directory.Root;
    public string Project { get; }
    public string Source { get; }
    public string ArtifactParent { get; }
    public string ArtifactRoot => Context.ArtifactDirectory!;
    public ToolExecutionContext Context { get; }
    public CancellationToken Token => TestContext.Current.CancellationToken;
    public IArtifactFileSessionFactory Native { get; } = new WindowsArtifactFileSessionFactory(
        new WindowsAtomicFileWriteSessionFactory(), new WindowsAuthorizedDirectoryCreator());

    public OwnedArtifactFixture()
    {
        Project = _directory.DirectoryFor("project");
        Source = _directory.FileFor(Project);
        ArtifactParent = _directory.DirectoryFor("artifacts");
        var task = Guid.NewGuid();
        Context = new(task, Project, [Project], ApprovalPolicy.FullAccess,
            ArtifactDirectory: Path.Combine(ArtifactParent, task.ToString("N")))
        { ReadHandleValidator = new WindowsFileReadHandleValidator() };
    }

    public async Task<ToolResult> WriteAsync(IArtifactFileSessionFactory artifacts, ToolExecutionContext? context = null,
        CancellationToken? token = null, TimeProvider? time = null, IAtomicFileWriteSessionFactory? writes = null,
        string content = "updated")
    {
        var scope = context ?? Context;
        var version = await FileSnapshotReader.ReadAsync(Source, Token, Context);
        var writer = new AtomicTextFileWriter(new(), writes ?? new WindowsAtomicFileWriteSessionFactory(), artifactSessions: artifacts);
        var tool = new FileWriteTool(new(), writer);
        var executor = new ToolExecutor(new([tool]), new PolicyApprovalService(static (_, _, _) => ValueTask.FromResult(true)),
            timeProvider: time, readHandleValidator: new WindowsFileReadHandleValidator());
        return await executor.ExecuteAsync(new("owned-edit", tool.Descriptor.Name,
            JsonSerializer.SerializeToElement(new { path = Source, content, expectedSha256 = version.Sha256 })), scope, token ?? Token);
    }

    public void AssertNoArtifacts() => Assert.Empty(Directory.GetFiles(ArtifactRoot, "*", SearchOption.AllDirectories));
    public void Dispose() => _directory.Dispose();
}
