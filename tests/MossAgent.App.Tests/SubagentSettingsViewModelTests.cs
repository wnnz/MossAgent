using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SubagentSettingsViewModelTests
{
    [Fact]
    public async Task SaveCommand_PersistsPromptModelThinkingAndSharingSettings()
    {
        var provider = CreateProvider();
        var model = CreateModel(provider.Id);
        var repository = new TestSubagentConfigurationRepository();
        var viewModel = new SubagentSettingsViewModel(
            repository, new TestSubagentModelConfigurationRepository(provider, model));
        viewModel.NewCommand.Execute(null);
        viewModel.Name = "review";
        viewModel.TriggerCondition = "独立审查代码改动";
        viewModel.SystemPrompt = "只列出具体问题。";
        viewModel.SelectedModel = Assert.Single(viewModel.Models);
        Assert.True(viewModel.HasModels);
        Assert.False(viewModel.HasNoModels);
        viewModel.ReasoningEffort = "high";
        viewModel.ShareContext = false;

        await viewModel.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(repository.Profiles);
        Assert.Equal("review", saved.Name);
        Assert.Equal("独立审查代码改动", saved.TriggerCondition);
        Assert.Equal("只列出具体问题。", saved.SystemPrompt);
        Assert.Equal(model.Id, saved.ModelProfileId);
        Assert.Equal("high", saved.ReasoningEffort);
        Assert.False(saved.ShareContext);
        Assert.True(saved.IsEnabled);
    }

    [Fact]
    public async Task SaveCommand_RejectsCaseInsensitiveDuplicateNames()
    {
        var provider = CreateProvider();
        var model = CreateModel(provider.Id);
        var repository = new TestSubagentConfigurationRepository();
        var viewModel = new SubagentSettingsViewModel(
            repository, new TestSubagentModelConfigurationRepository(provider, model));
        viewModel.NewCommand.Execute(null);
        viewModel.Name = "review";
        viewModel.TriggerCondition = "审查";
        viewModel.SystemPrompt = "检查并汇报。";
        await viewModel.SaveCommand.ExecuteAsync(null);
        viewModel.NewCommand.Execute(null);
        viewModel.Name = "REVIEW";
        viewModel.TriggerCondition = "审查";
        viewModel.SystemPrompt = "检查并汇报。";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Single(repository.Profiles);
        Assert.Contains("不能重复", viewModel.Status);
    }

    private static AiProvider CreateProvider() =>
        new(Guid.NewGuid(), "test", ProviderProtocol.OpenAiResponses,
            new Uri("https://example.test/v1"), "test-key", null, false, true,
            new Dictionary<string, string>());

    private static ModelProfile CreateModel(Guid providerId) =>
        new(Guid.NewGuid(), providerId, "model", "Test model", 16_000, 2_000,
            "medium", null, null, false, false, false, true, "{}");
}
