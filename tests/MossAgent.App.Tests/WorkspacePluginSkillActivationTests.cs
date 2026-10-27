using MossAgent.App.ViewModels;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspacePluginSkillActivationTests
{
    [Fact]
    public async Task ActivatedSkillInstructionsReachTheRunRequest()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        using var state = fixture.CreateState();
        var activator = new RecordingPluginSkillActivator("### demo v1.0 — skills/a/SKILL.md\nbounded body");
        var runner = new HistoryCaptureAgentRunner();
        var executor = new WorkspaceRunExecutor(fixture.Repository, runner, fixture.Factory, skillActivator: activator);

        await executor.ExecuteAsync(state, fixture.Panels.CreateContext(state.Task, fixture.Project),
            _ => Task.CompletedTask, fixture.Token);

        Assert.Equal("### demo v1.0 — skills/a/SKILL.md\nbounded body", runner.Request!.PluginSkillInstructions);
        Assert.Equal([state.Task.Id], activator.TaskIds);
    }

    [Fact]
    public async Task ReviewChatsNeverInvokeTheActivator()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        var account = await fixture.Get<ISourceAccountRepository>().SaveAsync(
            new SourceAccount(Guid.NewGuid(), "review fixture", new Uri("https://enterprise.example.test/api/v3/"),
                "synthetic-review-credential", null, true), fixture.Token);
        var scope = new SourceRepositoryScope(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo", false);
        await fixture.Get<ISourceRepositoryGrants>().AddAsync(scope, fixture.Token);
        var reviewTask = await fixture.Get<ISourceReviewChats>().OpenAsync(
            new SourceReviewChatBinding(Guid.NewGuid(), scope,
                new PullRequestRevision(7, new string('a', 40), new string('b', 40)),
                "untrusted review context"), "review chat", fixture.Token);
        var activator = new RecordingPluginSkillActivator("must never reach a review prompt");
        var runner = new HistoryCaptureAgentRunner();
        var executor = new WorkspaceRunExecutor(fixture.Repository, runner, fixture.Factory,
            reviewChats: fixture.Get<SourceReviewChatAccess>(), skillActivator: activator);
        var provider = new AiProvider(Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
            new Uri("https://fixture.example.test/"), "synthetic-key", null, true, true,
            new Dictionary<string, string>());
        var model = new ModelProfile(Guid.NewGuid(), provider.Id, "fixture", "fixture", 8192, 1024,
            "off", null, null, false, false, true, true, "{}");
        var assistant = new ChatMessageViewModel("MossAgent", string.Empty);
        using var state = new WorkspaceRunState(reviewTask, null, provider, model,
            [new ChatMessageViewModel("你", "look at this pull request"), assistant], assistant);

        await executor.ExecuteAsync(state, fixture.Panels.CreateContext(reviewTask, null),
            _ => Task.CompletedTask, fixture.Token);

        Assert.Empty(activator.TaskIds);
        Assert.Null(runner.Request!.PluginSkillInstructions);
    }
}
