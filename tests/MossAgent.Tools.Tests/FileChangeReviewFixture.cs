using System.Text;
using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Git;
using Xunit;

namespace MossAgent.Tools.Tests;

internal sealed class FileChangeReviewFixture : IDisposable
{
    private readonly FileEditingFixture _files = new();
    public string Root => _files.Root;
    public string Path => _files.Path;
    public CancellationToken Token => _files.Token;
    public FileChangeReviewTool Tool { get; }
    public ToolExecutionContext Context { get; }

    public FileChangeReviewFixture()
    {
        Context = _files.Context with { ArtifactDirectory = System.IO.Path.Combine(Root, "artifacts", _files.Context.TaskId.ToString("N")) };
        Tool = new(_files.Paths, new GitCommandRunner(new FixtureTaskProcessLauncher()));
    }

    public async Task<ToolFileChange> ChangeAsync(Encoding? encoding = null, bool create = false,
        string before = "原文\n保留\n", string after = "更新\n保留\n")
    {
        if (!create) await _files.SeedAsync(before, encoding);
        var result = create
            ? await _files.ExecuteAsync(_files.Write, new { path = Path, content = after }, Context)
            : await _files.ExecuteAsync(_files.Replace, new { path = Path, oldText = before, newText = after }, Context);
        Assert.True(result.IsSuccess, result.Summary);
        return Assert.Single(result.FileChanges!);
    }

    public Task<ToolResult> ReviewAsync(ToolFileChange change, ToolExecutionContext? context = null, bool approve = true,
        CancellationToken? token = null, bool trusted = true)
    {
        IToolApprovalService approvals = approve ? new PolicyApprovalService((_, _, _) => ValueTask.FromResult(true))
            : new PreviewDenyApprovalService();
        var executor = new ToolExecutor(new ToolRegistry([Tool]), approvals);
        var scope = (context ?? Context) with { ReviewedChange = trusted ? change : context?.ReviewedChange };
        return executor.ExecuteAsync(new(Guid.NewGuid().ToString("N"), Tool.Descriptor.Name,
            JsonSerializer.SerializeToElement(new { change })), scope, token ?? Token);
    }

    public void AssertNoReviewTemporaryDirectories() => Assert.Empty(Directory.GetDirectories(Context.ArtifactDirectory!, "review-*"));
    public void Dispose() => _files.Dispose();
}
