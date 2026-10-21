using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class MoveHandleFixture : IDisposable
{
    private readonly ReadHandleDirectoryFixture _directory = new();
    public string Root => _directory.Root;
    public string Authorized { get; }
    public string Outside { get; }
    public string SourceParent { get; }
    public string Source { get; }
    public string TargetParent { get; }
    public string Target => Path.Combine(TargetParent, "moved.txt");
    public ToolExecutionContext Context { get; }
    public CancellationToken Token => Xunit.TestContext.Current.CancellationToken;
    public CallbackPathMoveSessions Sessions { get; }
    public PathMoveTool Tool { get; }

    public MoveHandleFixture(CallbackPathMoveSessions? sessions = null, IFileReadHandleValidator? validator = null)
    {
        Authorized = _directory.DirectoryFor("authorized");
        Outside = _directory.DirectoryFor("outside");
        SourceParent = _directory.DirectoryFor("authorized/source");
        Source = _directory.FileFor(SourceParent);
        TargetParent = _directory.DirectoryFor("authorized/target");
        Sessions = sessions ?? new();
        Tool = new(new(), Sessions, new WindowsAuthorizedDirectoryCreator());
        Context = new(Guid.NewGuid(), Authorized, [Authorized], ApprovalPolicy.FullAccess)
        { ReadHandleValidator = validator ?? new WindowsFileReadHandleValidator() };
    }

    public Task<ToolResult> MoveAsync(string? source = null, string? target = null, bool overwrite = false,
        ToolExecutionContext? context = null, CancellationToken? token = null, bool approve = true, TimeProvider? time = null) =>
        new ToolExecutor(new([Tool]), new PolicyApprovalService((_, _, _) => ValueTask.FromResult(approve)),
            timeProvider: time, readHandleValidator: Context.ReadHandleValidator)
            .ExecuteAsync(Request(source ?? Source, target ?? Target, overwrite), context ?? Context, token ?? Token);

    public static ToolRequest Request(string source, string destination, bool overwrite = false) =>
        new(Guid.NewGuid().ToString("N"), "filesystem.move_path", JsonSerializer.SerializeToElement(new { source, destination, overwrite }));

    public void Dispose() => _directory.Dispose();
}
