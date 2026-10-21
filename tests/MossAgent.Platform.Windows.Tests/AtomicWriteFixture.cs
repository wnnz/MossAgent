using System.Text;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class AtomicWriteFixture : IDisposable
{
    private readonly ReadHandleDirectoryFixture _directory = new();
    public string Root => _directory.Root;
    public string Parent { get; }
    public string Path => System.IO.Path.Combine(Parent, "file.txt");
    public CancellationToken Token => TestContext.Current.CancellationToken;
    public ToolExecutionContext Context { get; }
    public WindowsAtomicFileWriteSessionFactory Sessions { get; } = new();
    public AtomicTextFileWriter Writer { get; }

    public AtomicWriteFixture()
    {
        Parent = _directory.DirectoryFor("parent");
        Context = new ToolExecutionContext(Guid.NewGuid(), Root, [Root], ApprovalPolicy.FullAccess)
        {
            ReadHandleValidator = new WindowsFileReadHandleValidator()
        };
        Writer = new AtomicTextFileWriter(new AuthorizedPathResolver(), Sessions);
    }

    public Task SeedAsync(string content = "original") => File.WriteAllTextAsync(Path, content, Token);
    public Task<FileSnapshot> SnapshotAsync() => FileSnapshotReader.ReadAsync(Path, Token, Context);
    public Task<FileSnapshot> WriteAsync(FileSnapshot original, ToolExecutionContext? context = null) =>
        Writer.WriteTextAsync(Path, "updated", new UTF8Encoding(false), original, context ?? Context, Token);
    public void AssertNoTemporaryFiles() => Assert.Empty(Directory.GetFiles(Root, ".mossagent-*.tmp", SearchOption.AllDirectories));
    public void Dispose() => _directory.Dispose();
}
