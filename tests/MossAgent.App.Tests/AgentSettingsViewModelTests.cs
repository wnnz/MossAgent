using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class AgentSettingsViewModelTests
{
    [Fact]
    public async Task SaveAndDelete_PersistMultipleAgentsAndNotifyWorkspace()
    {
        var provider = CreateProvider();
        var model = CreateModel(provider.Id);
        var repository = new TestAgentConfigurationRepository();
        var viewModel = new AgentSettingsViewModel(
            repository, new TestSubagentModelConfigurationRepository(provider, model));
        var notificationCount = 0;
        viewModel.ProfilesChanged += () =>
        {
            notificationCount++;
            return Task.CompletedTask;
        };

        await SaveProfileAsync(viewModel, "审查代理", "只报告缺陷。", "high");
        await SaveProfileAsync(viewModel, "实现代理", "实现并测试。", "medium");

        Assert.Equal(2, repository.Profiles.Count);
        Assert.All(repository.Profiles, profile => Assert.Equal(model.Id, profile.ModelProfileId));
        Assert.Equal(2, notificationCount);

        viewModel.SelectedProfile = viewModel.Items.Single(item => item.Name == "审查代理");
        await viewModel.DeleteCommand.ExecuteAsync(null);

        Assert.Single(repository.Profiles);
        Assert.Equal("实现代理", repository.Profiles[0].Name);
        Assert.Equal(3, notificationCount);
    }

    [Fact]
    public async Task SaveCommand_RejectsDuplicateNameAndOversizedPrompt()
    {
        var provider = CreateProvider();
        var model = CreateModel(provider.Id);
        var repository = new TestAgentConfigurationRepository();
        var viewModel = new AgentSettingsViewModel(
            repository, new TestSubagentModelConfigurationRepository(provider, model));
        await SaveProfileAsync(viewModel, "review", "检查代码。", "high");

        viewModel.NewCommand.Execute(null);
        viewModel.Name = "REVIEW";
        viewModel.SystemPrompt = "检查代码。";
        await viewModel.SaveCommand.ExecuteAsync(null);
        Assert.Contains("不能重复", viewModel.Status);

        viewModel.Name = "other";
        viewModel.SystemPrompt = new string('a', 16_001);
        await viewModel.SaveCommand.ExecuteAsync(null);
        Assert.Contains("16000", viewModel.Status);
        Assert.Single(repository.Profiles);
    }

    private static async Task SaveProfileAsync(
        AgentSettingsViewModel viewModel, string name, string prompt, string reasoning)
    {
        viewModel.NewCommand.Execute(null);
        viewModel.Name = name;
        viewModel.SystemPrompt = prompt;
        viewModel.SelectedModel = Assert.Single(viewModel.Models);
        viewModel.ReasoningEffort = reasoning;
        await viewModel.SaveCommand.ExecuteAsync(null);
    }

    private static AiProvider CreateProvider() =>
        new(Guid.NewGuid(), "test", ProviderProtocol.OpenAiResponses,
            new Uri("https://example.test/v1"), "test-key", null, false, true,
            new Dictionary<string, string>());

    private static ModelProfile CreateModel(Guid providerId) =>
        new(Guid.NewGuid(), providerId, "model", "Test model", 16_000, 2_000,
            "medium", null, null, false, false, false, true, "{}");
}
