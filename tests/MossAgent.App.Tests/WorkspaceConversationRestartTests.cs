using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceConversationRestartTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ExplicitModelChoiceWithoutAnotherMessage_SurvivesRestart(bool customAgent, bool followAgentDefaults)
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        var providerId = Guid.NewGuid();
        var preferredId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        try
        {
            await using (var first = AppComposition.CreateServices(root))
            {
                await first.GetRequiredService<IDatabaseInitializer>().InitializeAsync(TestContext.Current.CancellationToken);
                await SeedAsync(first, root, providerId, preferredId, taskId);
                if (customAgent)
                {
                    var catalog = first.GetRequiredService<WorkspaceCatalogViewModel>();
                    await catalog.ReloadAsync();
                    catalog.SelectedTask = Assert.Single(catalog.Tasks);
                }
                var workspace = first.GetRequiredService<WorkspaceViewModel>();
                await OpenAsync(workspace, taskId);
                Assert.True(workspace.ModelPicker.TrySelect(providerId, preferredId));
                if (customAgent)
                {
                    var agent = new AgentProfile(agentId, "fixture-agent", "synthetic instructions", providerId, preferredId, "low", true);
                    await first.GetRequiredService<IAgentConfigurationRepository>().SaveAsync(agent, TestContext.Current.CancellationToken);
                    await workspace.ReloadAgentsAsync();
                    workspace.SelectedAgent = agent;
                }
                if (!followAgentDefaults) workspace.ModelPicker.SelectedReasoning = "高";
                await workspace.ShutdownAsync();
                if (followAgentDefaults)
                {
                    var agents = first.GetRequiredService<IAgentConfigurationRepository>();
                    var selected = Assert.Single(await agents.GetAllAsync(TestContext.Current.CancellationToken));
                    await agents.SaveAsync(selected with { ReasoningEffort = "ultra" }, TestContext.Current.CancellationToken);
                }
            }

            await using var restarted = AppComposition.CreateServices(root);
            await restarted.GetRequiredService<IDatabaseInitializer>().InitializeAsync(TestContext.Current.CancellationToken);
            var restored = restarted.GetRequiredService<WorkspaceViewModel>();
            await OpenAsync(restored, taskId);
            await WaitUntilAsync(() => restored.SelectedModel?.Id == preferredId);

            Assert.Equal(preferredId, restored.SelectedModel?.Id);
            Assert.Equal(followAgentDefaults ? "ultra" : "high", restored.ModelPicker.EffectiveReasoningEffort);
            Assert.Equal(followAgentDefaults ? "Ultra" : "高", restored.ModelPicker.SelectedReasoning);
            Assert.Equal(customAgent ? agentId : (Guid?)null, restored.SelectedAgent?.Id);
            await restored.LoadAsync();
            Assert.Equal(followAgentDefaults ? "ultra" : "high", restored.ModelPicker.EffectiveReasoningEffort);
            Assert.Equal(AgentTaskStatus.Completed, restored.Catalog.SelectedTask?.Status);
            Assert.Equal("history", Assert.Single(restored.Messages).Content);
            await restored.ShutdownAsync();
        }
        finally
        {
            SqliteTestPool.Clear(root);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static async Task OpenAsync(WorkspaceViewModel workspace, Guid taskId)
    {
        await workspace.LoadAsync();
        workspace.Catalog.SelectedTask = Assert.Single(workspace.Catalog.Tasks, task => task.Id == taskId);
        await WaitUntilAsync(() => workspace.ModelPicker.CanSendWithSelectedModel);
        Assert.True(workspace.ModelPicker.CanSendWithSelectedModel);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition() && watch.Elapsed < TimeSpan.FromSeconds(3))
            await Task.Delay(10, TestContext.Current.CancellationToken);
    }

    private static async Task SeedAsync(ServiceProvider services, string root, Guid providerId, Guid modelId, Guid taskId)
    {
        var token = TestContext.Current.CancellationToken;
        var configurations = services.GetRequiredService<IConfigurationRepository>();
        var provider = new AiProvider(providerId, "fixture", ProviderProtocol.OpenAiResponses,
            new Uri("https://fixture.example.test/v1/"), "synthetic-key", null, true, true, new Dictionary<string, string>());
        var model = new ModelProfile(Guid.NewGuid(), providerId, "default-model", "default-model", 16384, 2048,
            "medium", null, null, false, false, true, true, "{}");
        await configurations.SaveProviderAsync(provider, token);
        await configurations.SaveModelAsync(model, token);
        await configurations.SaveModelAsync(model with { Id = modelId, ModelId = "preferred-model", IsDefault = false }, token);
        var workspaces = services.GetRequiredService<IWorkspaceRepository>();
        var now = DateTimeOffset.UtcNow;
        var project = new ProjectProfile(Guid.NewGuid(), "fixture", root, [root], now);
        var task = new AgentTask(taskId, project.Id, "conversation", ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed, now, now, null);
        await workspaces.SaveProjectAsync(project, token);
        await workspaces.SaveTaskWithMessagesAsync(task,
            [new ConversationMessage(Guid.NewGuid(), taskId, MessageRole.User, "history", now)], token);
    }
}
