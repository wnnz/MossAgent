using System.Text.Json;
using MossAgent.App.ViewModels;
using MossAgent.Application.Agent;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceToolMessageSerializationTests
{
    private const string Answer = "## 结果\n\n```csharp\nvar value = 42;\n```\n\n| 项目 | 状态 |\n| --- | --- |\n| 文件 | 完成 |";

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void Serialize_WritesOnlyRawToolData(bool isSuccess, bool hasDialogue)
    {
        var tool = CreateEvent(isSuccess, hasDialogue ? CreateDialogue() : null, "原始详情");

        using var document = JsonDocument.Parse(WorkspaceToolMessageSerializer.Serialize(tool));
        var root = document.RootElement;

        Assert.Equal(new[] { "Details", "Dialogue", "ErrorCode", "IsSuccess", "Summary", "ToolName" },
            root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("原始详情", root.GetProperty("Details").GetString());
        Assert.Equal(isSuccess, root.GetProperty("IsSuccess").GetBoolean());
        if (!hasDialogue)
        {
            Assert.Equal(JsonValueKind.Null, root.GetProperty("Dialogue").ValueKind);
            return;
        }
        var messages = root.GetProperty("Dialogue").EnumerateArray().ToArray();
        Assert.Equal(4, messages.Length);
        Assert.Equal(Answer, messages[3].GetProperty("Content").GetString());
        Assert.True(messages[1].GetProperty("IsPartial").GetBoolean());
        Assert.All(messages, message => Assert.Equal(new[] { "Content", "IsPartial", "Role" },
            message.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)));
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(true, "")]
    [InlineData(false, null)]
    [InlineData(false, "失败详情")]
    public void RoundTrip_PreservesDialogueAndFailureMetadata(bool isSuccess, string? details)
    {
        var dialogue = CreateDialogue();
        var tool = CreateEvent(isSuccess, dialogue, details);
        var content = WorkspaceToolMessageSerializer.Serialize(tool);

        Assert.True(WorkspaceToolMessageSerializer.TryDeserialize(content, out var payload));
        Assert.Equal(tool.ToolName, payload!.ToolName);
        Assert.Equal(isSuccess, payload.IsSuccess);
        Assert.Equal(tool.Result.Summary, payload.Summary);
        Assert.Equal(details, payload.Details);
        Assert.Equal(tool.Result.ErrorCode, payload.ErrorCode);
        Assert.Equal(dialogue, payload.Dialogue);
        Assert.True(payload.HasDialogue);
        Assert.False(payload.HasDetails);
        var entries = ChatMessageViewModel.CreateTool(payload).DialogueEntries;
        Assert.Equal(dialogue.Count, entries.Count);
        var answer = entries[^1];
        Assert.True(answer.IsAnswer);
        Assert.Equal(Answer, answer.Content);
        Assert.Contains(answer.Blocks, block => block is ChatCodeBlockViewModel);
        Assert.Contains(answer.Blocks, block => block is ChatTableBlockViewModel);
    }

    [Fact]
    public void RoundTrip_PreservesSubagentRunSnapshotAndStatus()
    {
        var run = CreateRun(SubagentRunStatus.Completed);
        var tool = CreateEvent(true, CreateDialogue(), "原始详情", run);

        var content = WorkspaceToolMessageSerializer.Serialize(tool);
        Assert.DoesNotContain("private-system-prompt", content);
        Assert.DoesNotContain("private-trigger-condition", content);
        Assert.DoesNotContain("private-owner-instance", content);
        Assert.True(WorkspaceToolMessageSerializer.TryDeserialize(content, out var payload));

        var restored = Assert.IsType<SubagentRun>(payload!.SubagentRun);
        Assert.True(payload.HasSubagentRun);
        Assert.Equal(run.Id, restored.Id);
        Assert.Equal(run.ParentRunId, restored.ParentRunId);
        Assert.Equal("review", restored.AgentName);
        Assert.Equal("model", restored.ModelId);
        Assert.Equal(SubagentRunStatus.Completed, restored.Status);
        Assert.Contains("已完成", payload.SubagentRunIdentity);
        Assert.Equal(Answer, payload.Dialogue![^1].Content);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyPayload_DerivesPresentationFromRawData(bool hasDialogue)
    {
        var legacy = JsonSerializer.Serialize(new
        {
            ToolName = "subagent.run",
            IsSuccess = true,
            Summary = "子代理已完成",
            Details = "原始详情",
            Dialogue = hasDialogue ? CreateDialogue() : null,
            HasDialogue = !hasDialogue,
            HasDetails = hasDialogue,
            DialogueEntries = new[] { new { Role = "子代理", Content = "过时的重复展示内容" } }
        });
        var stored = new ConversationMessage(Guid.NewGuid(), Guid.NewGuid(), MessageRole.Tool,
            legacy, DateTimeOffset.UtcNow, "call-legacy");

        var message = WorkspaceConversationMapper.ToChatMessage(stored);

        Assert.True(message.IsSubagentTool);
        Assert.Equal("call-legacy", message.ToolCallId);
        Assert.Equal(hasDialogue, message.Tool!.HasDialogue);
        Assert.Equal(!hasDialogue, message.Tool.HasDetails);
        Assert.Equal(hasDialogue ? 4 : 0, message.DialogueEntries.Count);
        if (hasDialogue) Assert.Equal(Answer, message.DialogueEntries[^1].Content);
        message.ToggleToolCommand.Execute(null);
        var expanded = message.IsToolExpanded;
        message.ToggleToolCommand.Execute(null);
        Assert.NotEqual(expanded, message.IsToolExpanded);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"ToolName\":\" \"}")]
    [InlineData("{\"ToolName\":\"subagent.run\",\"Dialogue\":{}}")]
    public void InvalidPayload_IsStillRejected(string content)
    {
        Assert.False(WorkspaceToolMessageSerializer.TryDeserialize(content, out _));
    }

    private static IReadOnlyList<ToolDialogueMessage> CreateDialogue() =>
    [
        new("任务", "检查合成文件"),
        new("状态", "第 1 轮", true),
        new("工具 · filesystem.read_file", "原始结果"),
        new("子代理", Answer)
    ];

    private static AgentToolEvent CreateEvent(bool isSuccess, IReadOnlyList<ToolDialogueMessage>? dialogue,
        string? details, SubagentRun? run = null) =>
        new("subagent.run", "call-serialization",
            new ToolResult(isSuccess, "工具运行结果", details, ErrorCode: isSuccess ? null : "tool_failed",
                Dialogue: dialogue, SubagentRun: run));

    private static SubagentRun CreateRun(SubagentRunStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        return new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "call",
            Guid.NewGuid(), "review", "private-trigger-condition", "private-system-prompt",
            Guid.NewGuid(), "provider",
            ProviderProtocol.OpenAiResponses, Guid.NewGuid(), "model", 128000, 4096,
            "high", true, "private-owner-instance", status,
            "finished", null, now, now, now);
    }
}
