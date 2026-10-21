using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class CopyHandleFixture : IDisposable
{
    private readonly ReadHandleDirectoryFixture _directory = new();
    public string Root => _directory.Root;
    public string Authorized { get; }
    public string Outside { get; }
    public string SourceDirectory { get; }
    public string Source { get; }
    public string DestinationDirectory { get; }
    public string Destination => Path.Combine(DestinationDirectory, "copied.txt");
    public ToolExecutionContext Context { get; }
    public CancellationToken Token => Xunit.TestContext.Current.CancellationToken;
    public CallbackFileTreeSessions Sessions { get; }
    public PathCopyTool Tool { get; }

    public CopyHandleFixture(CallbackFileTreeSessions? sessions = null, AtomicTextFileWriter? writer = null)
    {
        Authorized = _directory.DirectoryFor("authorized");
        Outside = _directory.DirectoryFor("outside");
        SourceDirectory = _directory.DirectoryFor("authorized/source");
        Source = _directory.FileFor(SourceDirectory);
        DestinationDirectory = _directory.DirectoryFor("authorized/target");
        Sessions = sessions ?? new();
        Tool = new(new(), writer ?? new(new(), new WindowsAtomicFileWriteSessionFactory()), Sessions,
            new WindowsAuthorizedDirectoryCreator());
        Context = new(Guid.NewGuid(), Authorized, [Authorized], ApprovalPolicy.FullAccess)
        { ReadHandleValidator = new WindowsFileReadHandleValidator() };
    }

    public Task<ToolResult> CopyAsync(string? source = null, string? destination = null, bool overwrite = true,
        ToolExecutionContext? context = null, CancellationToken? token = null) =>
        Executor().ExecuteAsync(Request(source ?? Source, destination ?? Destination, overwrite), context ?? Context, token ?? Token);

    public ToolExecutor Executor(bool approve = true, TimeProvider? time = null) => new(new([Tool]),
        new PolicyApprovalService((_, _, _) => ValueTask.FromResult(approve)), timeProvider: time,
        readHandleValidator: Context.ReadHandleValidator);

    public static ToolRequest Request(string source, string destination, bool overwrite = true) =>
        new(Guid.NewGuid().ToString("N"), "filesystem.copy_path", JsonSerializer.SerializeToElement(new { source, destination, overwrite }));

    public void Dispose() => _directory.Dispose();
}
