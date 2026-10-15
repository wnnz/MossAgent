using System.Text.Json;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceGeminiStreamFlowTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public async Task FailedStreams_DoNotExecuteOrReplayAnyTool(int scenario)
    {
        await using var fixture = await WorkspaceGeminiStreamFixture.CreateAsync((GeminiStreamScenario)scenario);
        await fixture.SendAsync(ApprovalPolicy.FullAccess);

        var task = fixture.Workspace.Catalog.SelectedTask!;
        Assert.Equal(AgentTaskStatus.Failed, task.Status);
        var reason = scenario switch
        {
            2 or 3 => TaskFailureReason.ProviderBlocked,
            4 => TaskFailureReason.ProviderOutputLimit,
            9 => TaskFailureReason.StreamLineLimit,
            10 => TaskFailureReason.StreamEventLimit,
            _ => TaskFailureReason.InvalidProviderResponse
        };
        Assert.Equal(reason, task.FailureReason);
        Assert.True(fixture.Workspace.IsSelectedTaskFailed);
        var hint = fixture.Workspace.TaskFailureHint;
        Assert.NotEqual(WorkspaceText.Get("TaskFailureHint"), hint);
        Assert.False(fixture.Workspace.IsRunning);
        Assert.Single(fixture.Server.Requests);
        Assert.Empty(await fixture.Audits.GetRecentAsync(task.Id, 20, fixture.Token));
        Assert.Equal(GeminiStreamFlowResponses.Evidence, await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
        var messages = await fixture.Repository.GetMessagesAsync(task.Id, fixture.Token);
        Assert.Equal(GeminiStreamFlowResponses.PartialReply, Assert.Single(messages, message => message.Role == MessageRole.Assistant).Content);
        Assert.DoesNotContain(messages, message => message.Role == MessageRole.Tool);
        Assert.DoesNotContain(fixture.Workspace.Messages, message => message.IsTool);

        await fixture.ReopenAsync(task.Id);

        Assert.Equal(AgentTaskStatus.Failed, fixture.Workspace.Catalog.SelectedTask!.Status);
        Assert.Equal(reason, fixture.Workspace.Catalog.SelectedTask.FailureReason);
        Assert.Equal(hint, fixture.Workspace.TaskFailureHint);
        Assert.False(fixture.Workspace.IsRunning);
        Assert.Single(fixture.Server.Requests);
        Assert.Equal(messages.Select(message => message.Id), (await fixture.Repository.GetMessagesAsync(task.Id, fixture.Token)).Select(message => message.Id));
        Assert.Empty(await fixture.Audits.GetRecentAsync(task.Id, 20, fixture.Token));
        Assert.Equal(GeminiStreamFlowResponses.Evidence, await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
    }

    [Fact]
    public async Task SettingsToSelectedCandidate_PerformsReadAndPersistsFinalReply()
    {
        await using var fixture = await WorkspaceGeminiStreamFixture.CreateAsync(GeminiStreamScenario.SelectedCandidate);
        await fixture.SendAsync(ApprovalPolicy.ReadOnly);

        var task = fixture.Workspace.Catalog.SelectedTask!;
        Assert.Equal(AgentTaskStatus.Completed, task.Status);
        Assert.Null(task.FailureReason);
        Assert.Equal(2, fixture.Server.Requests.Count);
        Assert.All(fixture.Server.Requests, request => Assert.Equal(
            "POST /v1beta/models/gemini-2.5-flash:streamGenerateContent?alt=sse HTTP/1.1", request.Target));
        var first = fixture.Server.Requests[0].Body;
        var generation = first.GetProperty("generationConfig");
        Assert.Equal(4096, generation.GetProperty("maxOutputTokens").GetInt32());
        Assert.Equal(3072, generation.GetProperty("thinkingConfig").GetProperty("thinkingBudget").GetInt32());
        Assert.Equal(0.1m, generation.GetProperty("temperature").GetDecimal());
        Assert.Equal(0.8m, generation.GetProperty("topP").GetDecimal());
        Assert.True(first.GetProperty("systemInstruction").GetProperty("parts").GetArrayLength() > 0);
        var audit = Assert.Single(await fixture.Audits.GetRecentAsync(task.Id, 20, fixture.Token));
        Assert.Equal("filesystem.read_file", audit.ToolName);
        Assert.True(audit.IsSuccess);
        Assert.Equal(ApprovalPolicy.ReadOnly, audit.ApprovalPolicy);
        var second = fixture.Server.Requests[1].Body;
        var response = Assert.Single(second.GetProperty("contents").EnumerateArray()
            .SelectMany(content => content.GetProperty("parts").EnumerateArray()),
            part => part.TryGetProperty("functionResponse", out _)).GetProperty("functionResponse");
        Assert.StartsWith("filesystem_read_file__", response.GetProperty("name").GetString());
        Assert.Equal(GeminiStreamFlowResponses.Evidence, response.GetProperty("response").GetProperty("Content").GetString());
        Assert.DoesNotContain(GeminiStreamFlowResponses.RejectedContent, second.GetRawText());
        Assert.DoesNotContain("不能合并的替代候选", second.GetRawText());
        var messages = await fixture.Repository.GetMessagesAsync(task.Id, fixture.Token);
        Assert.Single(messages, message => message.Role == MessageRole.Tool);
        Assert.Equal(GeminiStreamFlowResponses.FinalReply, messages.Last(message => message.Role == MessageRole.Assistant).Content);
        Assert.Equal(GeminiStreamFlowResponses.Evidence, await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));

        await fixture.ReopenAsync(task.Id);

        Assert.Equal(AgentTaskStatus.Completed, fixture.Workspace.Catalog.SelectedTask!.Status);
        Assert.Equal(2, fixture.Server.Requests.Count);
        Assert.Single(await fixture.Audits.GetRecentAsync(task.Id, 20, fixture.Token));
        Assert.Equal(messages.Select(message => message.Id), (await fixture.Repository.GetMessagesAsync(task.Id, fixture.Token)).Select(message => message.Id));
    }
}
