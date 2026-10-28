using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Git;
using MossAgent.Tools.BuiltIn.Processes;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class ToolPreflightTests
{
    [Fact]
    public async Task PathTools_RejectUnauthorizedArgumentsBeforeApproval()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var outside = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
            var resolver = new AuthorizedPathResolver();
            var git = new GitCommandRunner(new RejectingTaskProcessLauncher());
            var cases = new (IAgentTool Tool, object Arguments)[]
            {
                (new DirectoryCreateTool(resolver), new { path = outside }),
                (new DirectoryListTool(resolver), new { path = outside }),
                (new FileFindTool(resolver), new { path = outside }),
                (new FileReadTool(resolver), new { path = outside }),
                (new FileWriteTool(resolver), new { path = outside, content = "secret" }),
                (new PathCopyTool(resolver), new { source = root, destination = outside }),
                (new PathDeleteTool(resolver), new { path = outside }),
                (new PathDeleteTool(resolver), new { path = root }),
                (new PathMoveTool(resolver), new { source = root, destination = outside }),
                (new PathMoveTool(resolver), new { source = root, destination = Path.Combine(root, "moved") }),
                (new SearchTextTool(resolver, new RejectingTaskProcessLauncher()), new { query = "x", path = outside }),
                (new TextReplaceTool(resolver), new { path = outside, oldText = "x", newText = "y" }),
                (new GitStatusTool(resolver, git), new { repository = outside }),
                (new GitLogTool(resolver, git), new { repository = outside }),
                (new GitDiffTool(resolver, git), new { repository = outside }),
                (new GitDiffTool(resolver, git), new { repository = root, path = outside }),
                (new GitCreateWorktreeTool(resolver, git), new { repository = root, path = outside, branch = "test" }),
                (new ShellRunTool(resolver, new RejectingTaskProcessLauncher()),
                    new { command = "Write-Output hello", workingDirectory = outside })
            };
            var approvalRequests = 0;
            var executor = new ToolExecutor(
                new ToolRegistry(cases.Select(item => item.Tool).DistinctBy(tool => tool.Descriptor.Name)),
                new PolicyApprovalService((_, _, _) =>
                {
                    approvalRequests++;
                    return ValueTask.FromResult(true);
                }));
            var phases = new List<ToolExecutionPhase>();
            var context = new ToolExecutionContext(Guid.NewGuid(), root, [root], ApprovalPolicy.AskEveryTime)
            {
                Progress = report => phases.Add(report.Phase)
            };

            foreach (var (tool, arguments) in cases)
            {
                phases.Clear();
                var request = new ToolRequest("outside", tool.Descriptor.Name,
                    JsonSerializer.SerializeToElement(arguments));
                var result = await executor.ExecuteAsync(request, context, TestContext.Current.CancellationToken);
                Assert.Equal("path_denied", result.ErrorCode);
                Assert.Equal([ToolExecutionPhase.Validating, ToolExecutionPhase.Finalizing], phases);
            }

            Assert.Equal(0, approvalRequests);
            Assert.True(Directory.Exists(root));
            Assert.False(File.Exists(outside));
            Assert.False(Directory.Exists(outside));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ValidPathStillUsesApprovalAndExecutes()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var target = Path.Combine(root, "created.txt");
            var tool = new FileWriteTool(new AuthorizedPathResolver());
            var approvalRequests = 0;
            var executor = new ToolExecutor(
                new ToolRegistry([tool]),
                new PolicyApprovalService((_, _, _) =>
                {
                    approvalRequests++;
                    return ValueTask.FromResult(true);
                }));
            var context = new ToolExecutionContext(Guid.NewGuid(), root, [root], ApprovalPolicy.AskEveryTime);
            var request = new ToolRequest("inside", tool.Descriptor.Name,
                JsonSerializer.SerializeToElement(new { path = target, content = "created" }));

            var result = await executor.ExecuteAsync(request, context, TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess, result.Summary);
            Assert.Equal(1, approvalRequests);
            Assert.Equal("created", await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RevokedRootDuringApproval_IsRejectedBeforeExecution()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var roots = new List<string> { root };
            var target = Path.Combine(root, "must-not-exist.txt");
            var tool = new FileWriteTool(new AuthorizedPathResolver());
            var executor = new ToolExecutor(
                new ToolRegistry([tool]),
                new PolicyApprovalService((_, _, _) =>
                {
                    roots.Clear();
                    return ValueTask.FromResult(true);
                }));
            var context = new ToolExecutionContext(Guid.NewGuid(), root, roots, ApprovalPolicy.AskEveryTime);
            var request = new ToolRequest("revoked-root", tool.Descriptor.Name,
                JsonSerializer.SerializeToElement(new { path = target, content = "blocked" }));

            var result = await executor.ExecuteAsync(request, context, TestContext.Current.CancellationToken);

            Assert.Equal("path_denied", result.ErrorCode);
            Assert.False(File.Exists(target));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
