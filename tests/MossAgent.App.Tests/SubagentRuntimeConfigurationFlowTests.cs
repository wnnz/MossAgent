using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SubagentRuntimeConfigurationFlowTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ChangedConfigurationRejectsStaleEffortAndPersistsFailureWithoutChildRequest(
        bool changeProvider, bool shareContext)
    {
        await using var fixture = await SubagentProtocolFixture.CreateAsync(changeProvider
            ? ProviderProtocol.OpenAiResponses : ProviderProtocol.GeminiGenerateContent, shareContext);
        await ChangeConfigurationAsync(fixture, changeProvider);
        var configured = Assert.Single(fixture.Main.Subagents.Items);
        Assert.Equal(changeProvider ? "xhigh" : "high", configured.ReasoningEffort);

        await fixture.SendAsync();

        Assert.Equal(2, fixture.ParentServer.Requests.Count);
        Assert.Empty(fixture.ChildServer.Requests);
        var task = fixture.Workspace.Catalog.SelectedTask!;
        var audit = Assert.Single(await fixture.Audits.GetRecentAsync(task.Id, 10, fixture.Token));
        Assert.False(audit.IsSuccess);
        Assert.Equal("subagent_reasoning_unavailable", audit.ErrorCode);
        Assert.NotNull(audit.CompletedAt);
        AssertFailurePresentation(fixture.Workspace);
        await fixture.ReopenAsync(task.Id);
        AssertFailurePresentation(fixture.Workspace);
        Assert.Equal(2, fixture.ParentServer.Requests.Count);
        Assert.Empty(fixture.ChildServer.Requests);
        Assert.Equal(configured, Assert.Single(await fixture.Subagents.GetAllAsync(fixture.Token)));
    }

    [Fact]
    public async Task ExplicitlyRepairingEffortRestoresConfiguredChildWorkflow()
    {
        await using var fixture = await SubagentProtocolFixture.CreateAsync(ProviderProtocol.GeminiGenerateContent, false);
        await ChangeConfigurationAsync(fixture, changeProvider: false);
        var settings = fixture.Main.Subagents;
        settings.SelectedProfile = Assert.Single(settings.Items);
        settings.ReasoningEffort = "none";
        await settings.SaveCommand.ExecuteAsync(null);
        Assert.False(settings.HasStatusError, settings.Status);

        await fixture.SendAsync();

        Assert.Equal(2, fixture.ChildServer.Requests.Count);
        Assert.All(fixture.ChildServer.Requests, request =>
        {
            Assert.Contains("models/gemini-2.0-flash:streamGenerateContent", request.Target);
            Assert.False(request.Body.GetProperty("generationConfig").TryGetProperty("thinkingConfig", out _));
        });
        SubagentProtocolFlowTests.AssertDialogue(fixture.Workspace);
        var audits = await fixture.Audits.GetRecentAsync(fixture.Workspace.Catalog.SelectedTask!.Id, 10, fixture.Token);
        Assert.Equal(2, audits.Count);
        Assert.All(audits, audit => Assert.True(audit.IsSuccess));
        Assert.Equal("none", Assert.Single(settings.Items).ReasoningEffort);
    }

    private static async Task ChangeConfigurationAsync(SubagentProtocolFixture fixture, bool changeProvider)
    {
        fixture.Main.Models.EditProviderModelCommand.Execute(fixture.ChildModel);
        fixture.Main.Models.ModelId = changeProvider ? "gemini-3.1-pro" : "gemini-2.0-flash";
        fixture.Main.Models.ReasoningEffort = changeProvider ? "medium" : "none";
        await fixture.Main.Models.SaveModelCommand.ExecuteAsync(null);
        Assert.False(fixture.Main.Models.HasStatusError, fixture.Main.Models.Status);
        if (!changeProvider) return;
        fixture.Main.Subagents.SelectedProfile = Assert.Single(fixture.Main.Subagents.Items);
        fixture.Main.Subagents.ReasoningEffort = "xhigh";
        await fixture.Main.Subagents.SaveCommand.ExecuteAsync(null);
        Assert.False(fixture.Main.Subagents.HasStatusError, fixture.Main.Subagents.Status);
        fixture.Main.Providers.EditProviderCommand.Execute(fixture.ChildProvider);
        fixture.Main.Providers.Protocol = ProviderProtocol.GeminiGenerateContent;
        await fixture.Main.Providers.SaveCommand.ExecuteAsync(null);
        Assert.False(fixture.Main.Providers.HasValidationError, fixture.Main.Providers.Status);
    }

    private static void AssertFailurePresentation(WorkspaceViewModel workspace)
    {
        var row = Assert.Single(workspace.Messages, message => message.IsSubagentTool);
        Assert.False(row.IsToolRunning);
        Assert.False(row.Tool!.IsSuccess);
        Assert.Contains("思考强度", row.Tool.Summary);
        Assert.DoesNotContain("synthetic-only-key", row.Tool.Summary);
        Assert.True(row.Tool.Dialogue is null || row.Tool.Dialogue.Count == 0);
        Assert.DoesNotContain(workspace.Messages, message => message.Content == SubagentProtocolResponses.ChildReply);
    }
}
