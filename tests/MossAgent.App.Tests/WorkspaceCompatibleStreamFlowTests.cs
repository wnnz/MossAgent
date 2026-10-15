using System.Text.Json;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceCompatibleStreamFlowTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task FailedStreams_PersistPartialReplyWithoutExecutingOrReplayingTools(int scenario)
    {
        await using var fixture = await WorkspaceCompatibleStreamFixture.CreateAsync((CompatibleStreamScenario)scenario);
        await fixture.SendAsync(ApprovalPolicy.FullAccess);

        var task = fixture.Workspace.Catalog.SelectedTask!;
        Assert.Equal(AgentTaskStatus.Failed, task.Status);
        Assert.True(fixture.Workspace.IsSelectedTaskFailed);
        var reason = scenario switch
        {
            4 => TaskFailureReason.StreamLineLimit,
            5 => TaskFailureReason.StreamEventLimit,
            _ => TaskFailureReason.InvalidProviderResponse
        };
        Assert.Equal(reason, task.FailureReason);
        var hint = fixture.Workspace.TaskFailureHint;
        Assert.NotEqual(WorkspaceText.Get("TaskFailureHint"), hint);
        Assert.False(fixture.Workspace.IsRunning);
        Assert.Single(fixture.Server.Requests);
        Assert.Empty(await fixture.Audits.GetRecentAsync(task.Id, 20, fixture.Token));
        Assert.Equal(CompatibleStreamFlowResponses.Evidence, await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
        var messages = await fixture.Repository.GetMessagesAsync(task.Id, fixture.Token);
        Assert.Equal(CompatibleStreamFlowResponses.PartialReply, Assert.Single(messages, message => message.Role == MessageRole.Assistant).Content);
        Assert.DoesNotContain(messages, message => message.Role == MessageRole.Tool);
        Assert.DoesNotContain(fixture.Workspace.Messages, message => message.IsTool);

        await fixture.ReopenAsync(task.Id);

        Assert.Equal(AgentTaskStatus.Failed, fixture.Workspace.Catalog.SelectedTask!.Status);
        Assert.Equal(reason, fixture.Workspace.Catalog.SelectedTask.FailureReason);
        Assert.Equal(hint, fixture.Workspace.TaskFailureHint);
        Assert.True(fixture.Workspace.IsSelectedTaskFailed);
        Assert.False(fixture.Workspace.IsRunning);
        Assert.Single(fixture.Server.Requests);
        var reopened = await fixture.Repository.GetMessagesAsync(task.Id, fixture.Token);
        Assert.Equal(messages.Select(message => message.Id), reopened.Select(message => message.Id));
        Assert.Equal(CompatibleStreamFlowResponses.PartialReply, Assert.Single(reopened, message => message.Role == MessageRole.Assistant).Content);
        Assert.Empty(await fixture.Audits.GetRecentAsync(task.Id, 20, fixture.Token));
        Assert.Equal(CompatibleStreamFlowResponses.Evidence, await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
    }

    [Fact]
    public async Task SelectedCandidate_ExecutesOnlyItsReadAndCompletesThePersistedFollowup()
    {
        await using var fixture = await WorkspaceCompatibleStreamFixture.CreateAsync(CompatibleStreamScenario.MultipleChoices);
        await fixture.SendAsync(ApprovalPolicy.ReadOnly);

        var task = fixture.Workspace.Catalog.SelectedTask!;
        Assert.Equal(AgentTaskStatus.Completed, task.Status);
        Assert.Null(task.FailureReason);
        Assert.Equal(2, fixture.Server.Requests.Count);
        Assert.All(fixture.Server.Requests, request => Assert.Equal("POST /v1/chat/completions HTTP/1.1", request.Target));
        var first = fixture.Server.Requests[0].Body;
        Assert.Equal("compatible-model", first.GetProperty("model").GetString());
        Assert.True(first.GetProperty("stream").GetBoolean());
        Assert.Equal(1024, first.GetProperty("max_tokens").GetInt32());
        Assert.Equal("high", first.GetProperty("reasoning_effort").GetString());
        Assert.Equal(0.1m, first.GetProperty("temperature").GetDecimal());
        Assert.Equal(0.8m, first.GetProperty("top_p").GetDecimal());
        var audit = Assert.Single(await fixture.Audits.GetRecentAsync(task.Id, 20, fixture.Token));
        Assert.Equal("filesystem.read_file", audit.ToolName);
        Assert.Equal("chosen-read", audit.CallId);
        Assert.True(audit.IsSuccess);
        Assert.Equal(ApprovalPolicy.ReadOnly, audit.ApprovalPolicy);
        var second = fixture.Server.Requests[1].Body;
        var followup = second.GetProperty("messages").EnumerateArray().ToArray();
        var observation = Assert.Single(followup, message => message.GetProperty("role").GetString() == "tool");
        using var result = JsonDocument.Parse(observation.GetProperty("content").GetString()!);
        Assert.Equal(CompatibleStreamFlowResponses.Evidence, result.RootElement.GetProperty("Content").GetString());
        Assert.DoesNotContain("alternate-write", second.GetRawText());
        Assert.DoesNotContain(followup, message => message.GetProperty("content").GetString()?.Contains("不能合并的替代候选", StringComparison.Ordinal) == true);
        var messages = await fixture.Repository.GetMessagesAsync(task.Id, fixture.Token);
        Assert.Single(messages, message => message.Role == MessageRole.Tool);
        Assert.Equal(CompatibleStreamFlowResponses.FinalReply, messages.Last(message => message.Role == MessageRole.Assistant).Content);
        Assert.Equal(CompatibleStreamFlowResponses.Evidence, await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));

        await fixture.ReopenAsync(task.Id);

        Assert.Equal(AgentTaskStatus.Completed, fixture.Workspace.Catalog.SelectedTask!.Status);
        Assert.Equal(2, fixture.Server.Requests.Count);
        Assert.Single(await fixture.Audits.GetRecentAsync(task.Id, 20, fixture.Token));
        Assert.Equal(messages.Select(message => message.Id), (await fixture.Repository.GetMessagesAsync(task.Id, fixture.Token)).Select(message => message.Id));
    }
}
