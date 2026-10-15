using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class GeminiParentSubagentFlowTests
{
    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses, false)]
    [InlineData(ProviderProtocol.OpenAiResponses, true)]
    [InlineData(ProviderProtocol.OpenAiCompatible, false)]
    [InlineData(ProviderProtocol.OpenAiCompatible, true)]
    [InlineData(ProviderProtocol.AnthropicMessages, false)]
    [InlineData(ProviderProtocol.AnthropicMessages, true)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, false)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, true)]
    public async Task SavedChildConfiguration_ReturnsThroughSignedGeminiParentAndRestoresWithoutReplay(
        ProviderProtocol protocol, bool shareContext)
    {
        await using var fixture = await CreateAsync(protocol, shareContext);
        await fixture.SendAsync();
        Assert.Equal(2, fixture.ParentServer.Requests.Count);
        Assert.Equal(2, fixture.ChildServer.Requests.Count);
        AssertParent(fixture);
        SubagentProtocolFlowTests.AssertChildRequest(fixture, shareContext);
        Assert.Contains(SubagentProtocolResponses.Evidence, fixture.ChildServer.Requests[1].Body.GetRawText());
        var output = GeminiParentSubagentResponses.ReadOutput(fixture.ParentServer.Requests[1].Body);
        Assert.True(output.GetProperty("IsSuccess").GetBoolean());
        Assert.Equal(SubagentProtocolResponses.ChildReply, output.GetProperty("Content").GetString());
        var task = fixture.Workspace.Catalog.SelectedTask!;
        var audits = await fixture.Audits.GetRecentAsync(task.Id, 10, fixture.Token);
        Assert.Equal(2, audits.Count);
        Assert.True(Assert.Single(audits, item => item.ToolName == "subagent.run").IsSuccess);
        Assert.True(Assert.Single(audits, item => item.ToolName == "filesystem.read_file").IsSuccess);
        SubagentProtocolFlowTests.AssertDialogue(fixture.Workspace);
        var messages = await fixture.Repository.GetMessagesAsync(task.Id, fixture.Token);
        var stored = WorkspaceConversationMapper.ToChatMessage(Assert.Single(messages, message => message.Role == MessageRole.Tool));
        Assert.Contains(stored.Tool!.Dialogue!, message => message.Content == SubagentProtocolResponses.ChildReply);
        await fixture.ReopenAsync(task.Id);
        SubagentProtocolFlowTests.AssertDialogue(fixture.Workspace);
        Assert.Equal(2, fixture.ParentServer.Requests.Count);
        Assert.Equal(2, fixture.ChildServer.Requests.Count);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses, false)]
    [InlineData(ProviderProtocol.OpenAiResponses, true)]
    [InlineData(ProviderProtocol.OpenAiCompatible, false)]
    [InlineData(ProviderProtocol.OpenAiCompatible, true)]
    [InlineData(ProviderProtocol.AnthropicMessages, false)]
    [InlineData(ProviderProtocol.AnthropicMessages, true)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, false)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, true)]
    public async Task DisabledChild_ReturnsFailureWithoutFallbackOrChildRequest(ProviderProtocol protocol, bool provider)
    {
        await using var fixture = await CreateAsync(protocol, true);
        await fixture.DisableChildAsync(provider);
        await fixture.SendAsync();
        AssertParent(fixture);
        Assert.Empty(fixture.ChildServer.Requests);
        Assert.False(GeminiParentSubagentResponses.ReadOutput(fixture.ParentServer.Requests[1].Body)
            .GetProperty("IsSuccess").GetBoolean());
        var audit = Assert.Single(await fixture.Audits.GetRecentAsync(fixture.Workspace.Catalog.SelectedTask!.Id, 10, fixture.Token));
        Assert.False(audit.IsSuccess);
        Assert.Equal(provider ? "subagent_provider_unavailable" : "subagent_model_unavailable", audit.ErrorCode);
        var row = Assert.Single(fixture.Workspace.Messages, message => message.IsSubagentTool);
        Assert.False(row.IsToolRunning);
        Assert.False(row.Tool!.IsSuccess);
        Assert.DoesNotContain(row.Tool.Dialogue ?? [], message => message.Content == SubagentProtocolResponses.ChildReply);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses)]
    [InlineData(ProviderProtocol.OpenAiCompatible)]
    [InlineData(ProviderProtocol.AnthropicMessages)]
    [InlineData(ProviderProtocol.GeminiGenerateContent)]
    public async Task OutsideFile_FailsBothAuditsAndCannotReachGeminiParent(ProviderProtocol protocol)
    {
        await using var fixture = await CreateAsync(protocol, false, denyPath: true);
        await fixture.SendAsync();
        AssertParent(fixture);
        Assert.Single(fixture.ChildServer.Requests);
        var output = GeminiParentSubagentResponses.ReadOutput(fixture.ParentServer.Requests[1].Body);
        Assert.False(output.GetProperty("IsSuccess").GetBoolean());
        Assert.DoesNotContain(SubagentProtocolResponses.PrivateEvidence, output.GetRawText());
        var audits = await fixture.Audits.GetRecentAsync(fixture.Workspace.Catalog.SelectedTask!.Id, 10, fixture.Token);
        Assert.Equal(2, audits.Count);
        Assert.All(audits, item => Assert.False(item.IsSuccess));
        var row = Assert.Single(fixture.Workspace.Messages, message => message.IsSubagentTool);
        Assert.False(row.IsToolRunning);
        Assert.False(row.Tool!.IsSuccess);
        Assert.DoesNotContain(row.Tool.Dialogue!, message => message.Content == SubagentProtocolResponses.ChildReply);
        Assert.Equal(SubagentProtocolResponses.PrivateEvidence, await File.ReadAllTextAsync(fixture.OutsideFile, fixture.Token));
    }

    private static void AssertParent(SubagentProtocolFixture fixture)
    {
        Assert.Equal(2, fixture.ParentServer.Requests.Count);
        Assert.All(fixture.ParentServer.Requests, request =>
            Assert.StartsWith("POST /v1beta/models/" + fixture.ParentModelId + ":streamGenerateContent", request.Target));
        var first = fixture.ParentServer.Requests[0].Body;
        Assert.Equal("medium", first.GetProperty("generationConfig").GetProperty("thinkingConfig")
            .GetProperty("thinkingLevel").GetString()!.ToLowerInvariant());
        Assert.Contains("PROFILE_TRIGGER_MARKER", first.GetRawText());
        Assert.Contains(SubagentProtocolResponses.ParentMarker, first.GetRawText());
        var parts = GeminiParentSubagentResponses.ReadParts(fixture.ParentServer.Requests[1].Body);
        var call = Assert.Single(parts, part => part.TryGetProperty("functionCall", out _));
        Assert.Equal(GeminiParentSubagentResponses.Signature, call.GetProperty("thoughtSignature").GetString());
        Assert.Equal(GeminiParentSubagentResponses.NativeCallId, call.GetProperty("functionCall").GetProperty("id").GetString());
        Assert.Equal(GeminiParentSubagentResponses.NativeCallId, Assert.Single(parts, part => part.TryGetProperty("functionResponse", out _))
            .GetProperty("functionResponse").GetProperty("id").GetString());
    }

    private static Task<SubagentProtocolFixture> CreateAsync(ProviderProtocol protocol, bool shareContext, bool denyPath = false) =>
        SubagentProtocolFixture.CreateAsync(protocol, shareContext, denyPath, ProviderProtocol.GeminiGenerateContent);
}
