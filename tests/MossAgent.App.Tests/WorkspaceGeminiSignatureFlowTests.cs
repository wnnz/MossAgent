using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceGeminiSignatureFlowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SignedSteps_RoundTripHistoryAndNativeIdsWithoutPersistingOpaqueState(bool parallel)
    {
        await using var fixture = await WorkspaceGeminiStreamFixture.CreateAsync(parallel
            ? GeminiStreamScenario.SignedParallelSteps : GeminiStreamScenario.SignedSteps);
        await fixture.SendAsync(ApprovalPolicy.ReadOnly);

        var task = fixture.Workspace.Catalog.SelectedTask!;
        Assert.Equal(AgentTaskStatus.Completed, task.Status);
        Assert.Equal(3, fixture.Server.Requests.Count);
        Assert.All(fixture.Server.Requests, request => Assert.Contains("gemini-3-flash-preview", request.Target));
        Assert.Equal("HIGH", fixture.Server.Requests[0].Body.GetProperty("generationConfig")
            .GetProperty("thinkingConfig").GetProperty("thinkingLevel").GetString());
        var audits = await fixture.Audits.GetRecentAsync(task.Id, 20, fixture.Token);
        var toolCount = parallel ? 4 : 2;
        Assert.Equal(toolCount, audits.Count);
        Assert.All(audits, audit => Assert.Equal("filesystem.read_file", audit.ToolName));
        var messages = await fixture.Repository.GetMessagesAsync(task.Id, fixture.Token);
        Assert.Equal(toolCount, messages.Count(message => message.Role == MessageRole.Tool));
        Assert.All(messages, message =>
        {
            Assert.DoesNotContain("signature-", message.Content);
            Assert.DoesNotContain(GeminiSignatureFlowResponses.InternalThought, message.Content);
        });
        Assert.Equal(GeminiStreamFlowResponses.FinalReply, messages.Last(message => message.Role == MessageRole.Assistant).Content);
        Assert.Equal(GeminiStreamFlowResponses.Evidence, await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));

        await fixture.ReopenAsync(task.Id);

        Assert.Equal(AgentTaskStatus.Completed, fixture.Workspace.Catalog.SelectedTask!.Status);
        Assert.Equal(3, fixture.Server.Requests.Count);
        Assert.Equal(messages.Select(message => message.Id), (await fixture.Repository.GetMessagesAsync(task.Id, fixture.Token)).Select(message => message.Id));
        Assert.Equal(toolCount, (await fixture.Audits.GetRecentAsync(task.Id, 20, fixture.Token)).Count);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    public async Task InvalidContinuations_DiscardWholeToolBatchAndDoNotReplay(int scenario)
    {
        await using var fixture = await WorkspaceGeminiStreamFixture.CreateAsync((GeminiStreamScenario)scenario);
        await fixture.SendAsync(ApprovalPolicy.FullAccess);

        var task = fixture.Workspace.Catalog.SelectedTask!;
        Assert.Equal(AgentTaskStatus.Failed, task.Status);
        Assert.Equal(TaskFailureReason.InvalidProviderResponse, task.FailureReason);
        Assert.Single(fixture.Server.Requests);
        Assert.Empty(await fixture.Audits.GetRecentAsync(task.Id, 20, fixture.Token));
        Assert.Equal(GeminiStreamFlowResponses.Evidence, await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
        var messages = await fixture.Repository.GetMessagesAsync(task.Id, fixture.Token);
        Assert.DoesNotContain(messages, message => message.Role == MessageRole.Tool);

        await fixture.ReopenAsync(task.Id);

        Assert.Equal(AgentTaskStatus.Failed, fixture.Workspace.Catalog.SelectedTask!.Status);
        Assert.Single(fixture.Server.Requests);
        Assert.Empty(await fixture.Audits.GetRecentAsync(task.Id, 20, fixture.Token));
        Assert.Equal(GeminiStreamFlowResponses.Evidence, await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
    }
}
