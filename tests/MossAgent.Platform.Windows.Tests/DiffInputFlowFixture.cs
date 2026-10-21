using System.Text.Json;
using MossAgent.Testing;
using MossAgent.Tools.Tests;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Processes;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Git;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class DiffInputFlowFixture : IDisposable
{
    public OwnedArtifactFixture Files { get; } = new();
    public ObservedDiffInputSessions Inputs { get; } = new();
    public ToolFileChange Change { get; private set; } = null!;
    public ITaskProcessLauncher Processes { get; set; } = new FixtureTaskProcessLauncher();
    public int Approvals { get; private set; }

    public static async Task<DiffInputFlowFixture> CreateAsync(System.Text.Encoding? encoding = null,
        string before = "handle fixture", string after = "updated")
    {
        var fixture = new DiffInputFlowFixture();
        try
        {
            if (encoding is not null)
                await File.WriteAllBytesAsync(fixture.Files.Source, [.. encoding.GetPreamble(), .. encoding.GetBytes(before)], fixture.Files.Token);
            var result = await fixture.Files.WriteAsync(fixture.Files.Native, content: after);
            Xunit.Assert.True(result.IsSuccess, result.Summary);
            fixture.Change = Xunit.Assert.Single(result.FileChanges!);
            return fixture;
        }
        catch { fixture.Dispose(); throw; }
    }

    public Task<ToolResult> ExecuteAsync(bool statistics, ToolExecutionContext? context = null, CancellationToken? token = null,
        TimeProvider? time = null, bool approve = true)
    {
        var git = new GitCommandRunner(Processes);
        IAgentTool tool = statistics ? new FileChangeStatisticsTool(new(), git, Inputs)
            : new FileChangeReviewTool(new(), git, Files.Native, Inputs);
        IToolApprovalService approval = approve ? new PolicyApprovalService((_, _, _) =>
        { Approvals++; return ValueTask.FromResult(true); }) : new PreviewDenyApprovalService();
        var executor = new ToolExecutor(new([tool]), approval, timeProvider: time);
        return executor.ExecuteAsync(new(Guid.NewGuid().ToString("N"), tool.Descriptor.Name, JsonSerializer.SerializeToElement(new { change = Change })),
            (context ?? Files.Context) with { ReviewedChange = Change }, token ?? Files.Token);
    }

    public void Dispose() => Files.Dispose();
}
