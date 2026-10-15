using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceAgentSelectionTests
{
    [Fact]
    public async Task SelectingAgent_UsesItsModelAndThinkingAndManualModelChoiceClearsIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await using var services = AppComposition.CreateServices(root);
            await services.GetRequiredService<IDatabaseInitializer>()
                .InitializeAsync(cancellationToken);
            var configurations = services.GetRequiredService<IConfigurationRepository>();
            var provider = new AiProvider(Guid.NewGuid(), "test", ProviderProtocol.OpenAiResponses,
                new Uri("https://example.test/v1"), "test-key", null, true, true,
                new Dictionary<string, string>());
            var selectedModel = CreateModel(provider.Id, "selected");
            var otherModel = CreateModel(provider.Id, "other");
            await configurations.SaveProviderAsync(provider, cancellationToken);
            await configurations.SaveModelAsync(selectedModel, cancellationToken);
            await configurations.SaveModelAsync(otherModel, cancellationToken);
            var profile = new AgentProfile(Guid.NewGuid(), "review", "审查代码。",
                provider.Id, selectedModel.Id, "high", true);
            await services.GetRequiredService<IAgentConfigurationRepository>()
                .SaveAsync(profile, cancellationToken);

            var workspace = services.GetRequiredService<WorkspaceViewModel>();
            await workspace.LoadAsync();
            workspace.SelectAgentCommand.Execute(Assert.Single(workspace.Agents));

            Assert.Equal(profile.Id, workspace.SelectedAgent?.Id);
            Assert.Equal(selectedModel.Id, workspace.SelectedModel?.Id);
            Assert.Equal("high", workspace.ModelPicker.EffectiveReasoningEffort);

            Assert.True(workspace.ModelPicker.TrySelect(provider.Id, otherModel.Id));
            Assert.Null(workspace.SelectedAgent);
            Assert.Equal("默认代理", workspace.SelectedAgentLabel);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static ModelProfile CreateModel(Guid providerId, string modelId) =>
        new(Guid.NewGuid(), providerId, modelId, modelId, 16_000, 2_000,
            "medium", null, null, false, false, false, true, "{}");
}
