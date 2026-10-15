using System.Text.Json;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SubagentProtocolFlowTests
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
    public async Task SavedCrossProviderSubagent_RoutesToolsAndRestoresWithoutReplay(ProviderProtocol protocol, bool shareContext)
    {
        await using var fixture = await SubagentProtocolFixture.CreateAsync(protocol, shareContext);
        await fixture.SendAsync();
        Assert.Equal(2, fixture.ParentServer.Requests.Count);
        Assert.Equal(2, fixture.ChildServer.Requests.Count);
        Assert.All(fixture.ParentServer.Requests, item => Assert.Equal("parent-model", item.Body.GetProperty("model").GetString()));
        Assert.Equal("medium", fixture.ParentServer.Requests[0].Body.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Contains("PROFILE_TRIGGER_MARKER", fixture.ParentServer.Requests[0].Body.GetRawText());
        AssertChildRequest(fixture, shareContext);
        var followup = fixture.ChildServer.Requests[1].Body.GetRawText();
        Assert.Contains(SubagentProtocolResponses.Evidence, followup);
        Assert.DoesNotContain(SubagentProtocolResponses.PrivateEvidence, followup);
        using var parentOutput = JsonDocument.Parse(ReadParentOutput(fixture.ParentServer.Requests[1].Body));
        Assert.True(parentOutput.RootElement.GetProperty("IsSuccess").GetBoolean());
        Assert.Equal(SubagentProtocolResponses.ChildReply, parentOutput.RootElement.GetProperty("Content").GetString());
        var task = fixture.Workspace.Catalog.SelectedTask!;
        var audits = await fixture.Audits.GetRecentAsync(task.Id, 10, fixture.Token);
        Assert.True(Assert.Single(audits, item => item.ToolName == "subagent.run").IsSuccess);
        Assert.True(Assert.Single(audits, item => item.ToolName == "filesystem.read_file").IsSuccess);
        Assert.Equal(SubagentProtocolResponses.PrivateEvidence, await File.ReadAllTextAsync(fixture.OutsideFile, fixture.Token));
        AssertDialogue(fixture.Workspace);
        var messages = await fixture.Repository.GetMessagesAsync(task.Id, fixture.Token);
        var restored = WorkspaceConversationMapper.ToChatMessage(Assert.Single(messages, message => message.Role == MessageRole.Tool));
        Assert.True(restored.IsSubagentTool);
        Assert.Contains(restored.Tool!.Dialogue!, message => message.Content == SubagentProtocolResponses.ChildReply);
        await fixture.ReopenAsync(task.Id);
        AssertDialogue(fixture.Workspace);
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
    public async Task DisabledConfiguredChild_DoesNotFallbackOrStartAnyChildRequest(ProviderProtocol protocol, bool provider)
    {
        await using var fixture = await SubagentProtocolFixture.CreateAsync(protocol, true);
        await fixture.DisableChildAsync(provider);
        await fixture.SendAsync();
        Assert.Equal(2, fixture.ParentServer.Requests.Count);
        Assert.Empty(fixture.ChildServer.Requests);
        var task = fixture.Workspace.Catalog.SelectedTask!;
        var audit = Assert.Single(await fixture.Audits.GetRecentAsync(task.Id, 10, fixture.Token));
        Assert.False(audit.IsSuccess);
        Assert.Equal(provider ? "subagent_provider_unavailable" : "subagent_model_unavailable", audit.ErrorCode);
        var row = Assert.Single(fixture.Workspace.Messages, message => message.IsSubagentTool);
        Assert.False(row.IsToolRunning);
        Assert.False(row.Tool!.IsSuccess);
        Assert.DoesNotContain(fixture.Workspace.Messages, message => message.Content == SubagentProtocolResponses.ChildReply);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses)]
    [InlineData(ProviderProtocol.OpenAiCompatible)]
    [InlineData(ProviderProtocol.AnthropicMessages)]
    [InlineData(ProviderProtocol.GeminiGenerateContent)]
    public async Task UnauthorizedChildFile_IsAuditedAndStopsBeforeTheNextChildTurn(ProviderProtocol protocol)
    {
        await using var fixture = await SubagentProtocolFixture.CreateAsync(protocol, false, denyPath: true);
        await fixture.SendAsync();
        Assert.Equal(2, fixture.ParentServer.Requests.Count);
        Assert.Single(fixture.ChildServer.Requests);
        var task = fixture.Workspace.Catalog.SelectedTask!;
        var audits = await fixture.Audits.GetRecentAsync(task.Id, 10, fixture.Token);
        Assert.Equal(2, audits.Count);
        Assert.All(audits, item => Assert.False(item.IsSuccess));
        Assert.Single(audits, item => item.ToolName == "subagent.run");
        Assert.Single(audits, item => item.ToolName == "filesystem.read_file");
        var row = Assert.Single(fixture.Workspace.Messages, message => message.IsSubagentTool);
        Assert.False(row.IsToolRunning);
        Assert.False(row.Tool!.IsSuccess);
        Assert.DoesNotContain(SubagentProtocolResponses.PrivateEvidence, ReadParentOutput(fixture.ParentServer.Requests[1].Body));
        Assert.DoesNotContain(row.Tool.Dialogue!, message => message.Content == SubagentProtocolResponses.ChildReply);
        Assert.Equal(SubagentProtocolResponses.PrivateEvidence, await File.ReadAllTextAsync(fixture.OutsideFile, fixture.Token));
    }

    internal static void AssertChildRequest(SubagentProtocolFixture fixture, bool shareContext)
    {
        var (target, request) = fixture.ChildServer.Requests[0];
        var serialized = request.GetRawText();
        Assert.Contains("CHILD_SYSTEM_MARKER", serialized);
        Assert.Equal(shareContext, serialized.Contains(SubagentProtocolResponses.ParentMarker, StringComparison.Ordinal));
        Assert.DoesNotContain("subagent_run__", serialized);
        if (fixture.Protocol == ProviderProtocol.GeminiGenerateContent)
        {
            Assert.Contains("models/" + fixture.ChildModel.ModelId + ":streamGenerateContent", target);
            Assert.Equal("high", request.GetProperty("generationConfig").GetProperty("thinkingConfig").GetProperty("thinkingLevel").GetString()!.ToLowerInvariant());
            var parts = fixture.ChildServer.Requests[1].Body.GetProperty("contents").EnumerateArray()
                .SelectMany(message => message.GetProperty("parts").EnumerateArray()).ToArray();
            var call = Assert.Single(parts, part => part.TryGetProperty("functionCall", out _));
            Assert.Equal("c2lnbmVkLWFsbG93ZWQ=", call.GetProperty("thoughtSignature").GetString());
            Assert.Equal("child-read", Assert.Single(parts, part => part.TryGetProperty("functionResponse", out _))
                .GetProperty("functionResponse").GetProperty("id").GetString());
        }
        else
        {
            Assert.Equal(fixture.ChildModel.ModelId, request.GetProperty("model").GetString());
            Assert.StartsWith("POST /v1/" + (fixture.Protocol == ProviderProtocol.OpenAiCompatible ? "chat/completions"
                : fixture.Protocol == ProviderProtocol.OpenAiResponses ? "responses" : "messages"), target);
            if (fixture.Protocol == ProviderProtocol.OpenAiCompatible)
                Assert.Equal("high", request.GetProperty("reasoning_effort").GetString());
            else if (fixture.Protocol == ProviderProtocol.OpenAiResponses)
                Assert.Equal("high", request.GetProperty("reasoning").GetProperty("effort").GetString());
            else Assert.Equal("enabled", request.GetProperty("thinking").GetProperty("type").GetString());
        }
    }

    internal static void AssertDialogue(WorkspaceViewModel workspace)
    {
        var row = Assert.Single(workspace.Messages, message => message.IsSubagentTool);
        Assert.False(row.IsToolRunning);
        Assert.True(row.Tool!.IsSuccess);
        Assert.Contains(row.Tool.Dialogue!, message => message.Content == SubagentProtocolResponses.ChildReply);
        Assert.Contains(workspace.Messages, message => message.Content == SubagentProtocolResponses.ParentReply);
        row.ToggleToolCommand.Execute(null);
        Assert.False(row.IsToolExpanded);
        row.ToggleToolCommand.Execute(null);
        Assert.True(row.IsToolExpanded);
    }

    private static string ReadParentOutput(JsonElement request) => request.GetProperty("input").EnumerateArray()
        .Single(item => item.TryGetProperty("type", out var type) && type.GetString() == "function_call_output")
        .GetProperty("output").GetString()!;
}
