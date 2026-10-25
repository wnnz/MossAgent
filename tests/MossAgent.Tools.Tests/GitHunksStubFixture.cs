using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Processes;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Git;

namespace MossAgent.Tools.Tests;

internal sealed class GitHunksStubFixture : IDisposable
{
    private readonly Queue<ITaskProcess> _processes = new();
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
    public GitIndexRepositoryProbe Repository { get; } = new();
    public RecordingTaskProcessLauncher Launcher { get; }
    public ToolExecutor Executor { get; }
    public int ApprovalRequests { get; private set; }

    public GitHunksStubFixture()
    {
        Directory.CreateDirectory(Root);
        Launcher = new(() => _processes.Dequeue());
        var runner = new GitCommandRunner(Launcher);
        var paths = new GitIndexPaths(new AuthorizedPathResolver());
        var hunks = new GitHunkApplier(paths, Repository, runner);
        Executor = new(new ToolRegistry([new GitStageHunksTool(hunks), new GitUnstageHunksTool(hunks)]),
            new PolicyApprovalService((_, _, _) => { ApprovalRequests++; return ValueTask.FromResult(true); }));
    }

    public ToolExecutionContext Context => new(Guid.NewGuid(), Root, [Root], ApprovalPolicy.AskEveryTime,
        Timeout: TimeSpan.FromSeconds(5));

    public StubTaskProcess Enqueue(string standardOutput = "", int exitCode = 0, string standardError = "", TextWriter? input = null)
    {
        var process = new StubTaskProcess
        {
            ExitCode = exitCode,
            StandardInput = input,
            StandardOutput = new StringReader(standardOutput),
            StandardError = new StringReader(standardError)
        };
        _processes.Enqueue(process);
        return process;
    }

    public Task<ToolResult> ExecuteAsync(string tool, object arguments, CancellationToken token,
        ToolExecutionContext? context = null) =>
        Executor.ExecuteAsync(new(Guid.NewGuid().ToString("N"), tool, JsonSerializer.SerializeToElement(arguments)),
            context ?? Context, token);

    public void Dispose() => Directory.Delete(Root, recursive: true);
}
