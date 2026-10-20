using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Git;

namespace MossAgent.Tools.Tests;

internal sealed class GitIndexExecutionFixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
    public GitIndexRepositoryProbe Repository { get; } = new();
    public StubTaskProcess Process { get; } = new();
    public ManualToolTimeProvider Clock { get; } = new();
    public ToolProgressAuditSink Audit { get; } = new();
    public RecordingTaskProcessLauncher Launcher { get; }
    public ToolExecutor Executor { get; }
    public Action? OnApproval { get; set; }
    public int Approvals { get; private set; }
    public ToolExecutionContext Context => new(Guid.NewGuid(), Root, [Root], ApprovalPolicy.AskEveryTime,
        Timeout: TimeSpan.FromSeconds(5));

    public GitIndexExecutionFixture()
    {
        Directory.CreateDirectory(Root);
        Launcher = new(() => Process);
        var runner = new GitCommandRunner(Launcher);
        var paths = new GitIndexPaths(new AuthorizedPathResolver());
        Audit.Release.SetResult();
        Executor = new(new([new GitStageFileTool(paths, Repository, runner), new GitUnstageFileTool(paths, Repository, runner)]),
            new PolicyApprovalService((_, _, _) => { Approvals++; OnApproval?.Invoke(); return ValueTask.FromResult(true); }), Audit, Clock);
    }

    public Task<ToolResult> ExecuteAsync(string tool, CancellationToken token, ToolExecutionContext? context = null, object? arguments = null) =>
        Executor.ExecuteAsync(new(Guid.NewGuid().ToString("N"), tool, JsonSerializer.SerializeToElement(arguments ?? new { path = "selected.txt" })),
            context ?? Context, token);

    public void Dispose()
    {
        var temporary = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MossAgent.Tests")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(Root).StartsWith(temporary, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Fixture cleanup escaped its temporary parent.");
        Directory.Delete(Root, recursive: true);
    }
}
