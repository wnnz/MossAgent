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

internal sealed class FileChangeRestoreFixture : IDisposable
{
    private readonly FileEditingFixture _files = new();
    public string Root => _files.Root;
    public string Path => _files.Path;
    public CancellationToken Token => _files.Token;
    public AtomicTextFileWriter Writer => _files.Writer;
    public FileChangeRestoreTool Restore { get; }
    public FileChangeRemoveCreatedTool Remove { get; }
    public FileChangeReviewTool Review { get; }
    public ToolExecutionContext Context { get; }
    public int ApprovalRequests { get; private set; }

    public FileChangeRestoreFixture()
    {
        Context = _files.Context with { ArtifactDirectory = System.IO.Path.Combine(Root, "artifacts", _files.Context.TaskId.ToString("N")) };
        Restore = new(_files.Paths, _files.Writer);
        Remove = new(_files.Paths, _files.Writer);
        Review = new(_files.Paths, new GitCommandRunner(new FixtureTaskProcessLauncher()));
    }

    public async Task<ToolFileChange> ChangeAsync(byte[]? before = null, bool create = false,
        string? path = null, ToolExecutionContext? context = null)
    {
        path ??= Path;
        if (!create) await File.WriteAllBytesAsync(path, before ?? Encoding.UTF8.GetBytes("original\r\n"), Token);
        var result = await _files.ExecuteAsync(_files.Write, new { path, content = "agent update\r\n" }, context ?? Context);
        Assert.True(result.IsSuccess, result.Summary);
        return Assert.Single(result.FileChanges!);
    }

    public Task<ToolResult> ExecuteAsync(ToolFileChange change, bool remove = false, ToolExecutionContext? context = null,
        bool approve = true, CancellationToken? token = null, bool trusted = true, IToolExecutionAuditSink? audit = null,
        Func<ToolDescriptor, ToolRequest, CancellationToken, ValueTask<bool>>? prompt = null, TimeProvider? timeProvider = null)
    {
        var scope = context ?? Context with { ApprovalPolicy = ApprovalPolicy.AskEveryTime };
        scope = scope with { ReviewedChange = trusted ? change : scope.ReviewedChange };
        var approvals = new PolicyApprovalService((descriptor, request, cancellation) =>
        {
            ApprovalRequests++;
            return prompt?.Invoke(descriptor, request, cancellation) ?? ValueTask.FromResult(approve);
        });
        var executor = new ToolExecutor(new ToolRegistry([Restore, Remove, Review]), approvals, audit, timeProvider);
        return executor.ExecuteAsync(Request(change, remove ? Remove : Restore), scope, token ?? Token);
    }

    public static ToolRequest Request(ToolFileChange change, IAgentTool tool) =>
        new(Guid.NewGuid().ToString("N"), tool.Descriptor.Name, JsonSerializer.SerializeToElement(new { change }));

    public void AssertNoTemporaryFiles() => _files.AssertNoTemporaryFiles();
    public void Dispose() => _files.Dispose();
}
