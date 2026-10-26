using System.Text.Json;
using MossAgent.Agent;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Agent.Tests;

public sealed class SubagentFileChangeTests
{
    [Theory]
    [InlineData("success", null)]
    [InlineData("failure", "subagent_model_failed")]
    [InlineData("cancel", "tool_cancelled")]
    [InlineData("timeout", "tool_timeout")]
    [InlineData("observer", "subagent_run_failed")]
    public async Task CommittedChildChanges_SurviveAllParentEndings(string ending, string? errorCode)
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"))).FullName;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        try
        {
            var result = await ExecuteAsync(root, ending, true, cancellation);

            Assert.Equal(errorCode is null, result.IsSuccess);
            Assert.Equal(errorCode, result.ErrorCode);
            var change = Assert.Single(result.FileChanges!);
            Assert.Null(change.BeforeSha256);
            Assert.True(change.IsValid);
            Assert.Equal(Path.Combine(root, "child.txt"), change.Path);
            Assert.Equal("committed child content", await File.ReadAllTextAsync(change.Path, TestContext.Current.CancellationToken));
            Assert.DoesNotContain("private-fixture-error", result.Summary);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task DeniedChildWrite_ReportsNoChangeAndLeavesNoFile()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"))).FullName;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        try
        {
            var result = await ExecuteAsync(root, "success", false, cancellation);

            Assert.Equal("approval_denied", result.ErrorCode);
            Assert.Null(result.FileChanges);
            Assert.False(File.Exists(Path.Combine(root, "child.txt")));
        }
        finally { Directory.Delete(root, true); }
    }

    private static Task<ToolResult> ExecuteAsync(string root, string ending, bool approveChild, CancellationTokenSource cancellation)
    {
        var provider = new AiProvider(Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
            new Uri("https://example.test"), "synthetic-key", null, true, true, new Dictionary<string, string>());
        var model = new ModelProfile(Guid.NewGuid(), provider.Id, "child", "child", 16000, 2000,
            "off", null, null, false, false, true, true, "{}");
        var profile = new SubagentProfile(Guid.NewGuid(), "write", "编辑", "执行文件写入。", provider.Id, model.Id, "off", false, true);
        var registry = new StubModelProviderRegistry(new FileChangeSubagentModelProvider(Path.Combine(root, "child.txt"), ending, cancellation.Cancel));
        var tool = new SubagentTool(new InMemorySubagentConfigurationRepository([profile]), new InMemoryConfigurationRepository(provider, model), registry, new RecordingSubagentRunJournal());
        var childCatalog = new ToolRegistry([new FileWriteTool(new AuthorizedPathResolver())]);
        var childExecutor = new ToolExecutor(childCatalog, new FixedToolApprovalService(approveChild));
        var context = new ToolExecutionContext(Guid.NewGuid(), root, [root], ApprovalPolicy.FullAccess,
            Timeout: TimeSpan.FromSeconds(3), SubagentTools: new FileChangeSubagentGateway(childCatalog, childExecutor),
            SubagentProgress: message =>
            {
                if (ending == "observer" && message.Role.StartsWith("工具 · ", StringComparison.Ordinal))
                    throw new InvalidOperationException("private-fixture-error");
            });
        var executor = new ToolExecutor(new ToolRegistry([tool]), new FixedToolApprovalService(true));
        return executor.ExecuteAsync(new ToolRequest("parent-write", SubagentTool.ToolName,
            JsonSerializer.SerializeToElement(new { agent = "write", task = "写入授权合成文件" })), context, cancellation.Token);
    }
}
