using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AgentModelCatalogFlowTests
{
    [Fact]
    public Task FirstModelAddedInSettings_BecomesAvailableWithoutResettingDraft() => RunAsync(async (services, provider, _) =>
    {
        var window = services.GetRequiredService<MainWindowViewModel>();
        var agents = window.Agents;
        Assert.True(agents.HasNoModels);
        SetDraft(agents);
        window.Models.SelectedProvider = Assert.Single(window.Models.Providers);
        window.Models.NewModelCommand.Execute(null);
        window.Models.ModelId = "new-child";
        window.Models.DisplayName = "新子代理模型";

        await window.Models.SaveModelCommand.ExecuteAsync(null);

        var option = Assert.Single(agents.Models);
        Assert.Equal(provider.Id, option.Model.ProviderId);
        Assert.Equal("new-child", option.Model.ModelId);
        Assert.Same(option, agents.SelectedModel);
        Assert.True(agents.HasModels);
        AssertDraft(agents);
    }, includeModel: false);

    [Fact]
    public Task SelectedModelEditedInSettings_RefreshesSnapshotAndPreservesDraft() => RunAsync(async (services, _, model) =>
    {
        var window = services.GetRequiredService<MainWindowViewModel>();
        var agents = window.Agents;
        SetDraft(agents);
        window.Models.EditProviderModelCommand.Execute(model);
        window.Models.DisplayName = "更新后的模型";
        window.Models.ContextLength = 24576;

        await window.Models.SaveModelCommand.ExecuteAsync(null);

        Assert.Equal(model!.Id, agents.SelectedModel?.Model.Id);
        Assert.Equal(24576, agents.SelectedModel?.Model.ContextLength);
        Assert.Contains("更新后的模型", agents.SelectedModel!.Label);
        AssertDraft(agents);
    });

    [Theory]
    [InlineData("delete-model")]
    [InlineData("disable-model")]
    [InlineData("disable-provider")]
    public Task UnavailableSelection_IsRemovedWithoutSubstitution(string change) => RunAsync(async (services, provider, model) =>
    {
        var repository = services.GetRequiredService<IConfigurationRepository>();
        await repository.SaveModelAsync(CreateModel(provider.Id, "alternative"), TestContext.Current.CancellationToken);
        var window = services.GetRequiredService<MainWindowViewModel>();
        var agents = window.Agents;
        SetDraft(agents);
        agents.SelectedModel = Assert.Single(agents.Models, option => option.Model.Id == model!.Id);
        if (change == "delete-model")
            await window.Models.DeleteProviderModelCommand.ExecuteAsync(model);
        else if (change == "disable-model")
        {
            window.Models.EditProviderModelCommand.Execute(model);
            window.Models.IsEnabled = false;
            await window.Models.SaveModelCommand.ExecuteAsync(null);
        }
        else
        {
            window.Providers.SelectedItem = Assert.Single(window.Providers.Items);
            window.Providers.IsEnabled = false;
            await window.Providers.SaveCommand.ExecuteAsync(null);
        }

        Assert.Null(agents.SelectedModel);
        Assert.DoesNotContain(agents.Models, option => option.Model.Id == model!.Id);
        Assert.Contains("不可用", agents.Status);
        AssertDraft(agents);
    });

    [Fact]
    public Task Save_RevalidatesProviderInsteadOfPersistingAStaleModel() => RunAsync(async (services, provider, _) =>
    {
        var window = services.GetRequiredService<MainWindowViewModel>();
        SetDraft(window.Agents);
        await services.GetRequiredService<IConfigurationRepository>().SaveProviderAsync(
            provider with { IsEnabled = false }, TestContext.Current.CancellationToken);

        await window.Agents.SaveCommand.ExecuteAsync(null);

        Assert.Empty(await services.GetRequiredService<IAgentConfigurationRepository>()
            .GetAllAsync(TestContext.Current.CancellationToken));
        Assert.Null(window.Agents.SelectedModel);
        AssertDraft(window.Agents);
    });

    [Fact]
    public Task DisposedWindow_DoesNotReloadAgentModels() => RunAsync(async (services, _, model) =>
    {
        var window = services.GetRequiredService<MainWindowViewModel>();
        window.Dispose();
        window.Models.EditProviderModelCommand.Execute(model);
        window.Models.DisplayName = "关闭后更新";

        await window.Models.SaveModelCommand.ExecuteAsync(null);

        Assert.DoesNotContain(window.Agents.Models, option => option.Label.Contains("关闭后更新"));
    });

    private static void SetDraft(AgentSettingsViewModel agents)
    {
        agents.NewCommand.Execute(null);
        agents.Name = "review-draft";
        agents.SystemPrompt = "保留尚未保存的提示词。";
        agents.ReasoningEffort = "high";
        agents.IsEnabled = false;
    }

    private static void AssertDraft(AgentSettingsViewModel agents)
    {
        Assert.Null(agents.SelectedProfile);
        Assert.Equal("review-draft", agents.Name);
        Assert.Equal("保留尚未保存的提示词。", agents.SystemPrompt);
        Assert.Equal("high", agents.ReasoningEffort);
        Assert.False(agents.IsEnabled);
    }

    private static ModelProfile CreateModel(Guid providerId, string modelId) =>
        new(Guid.NewGuid(), providerId, modelId, modelId, 16384, 2048, "medium",
            null, null, false, false, false, true, "{}");

    private static async Task RunAsync(Func<ServiceProvider, AiProvider, ModelProfile?, Task> action, bool includeModel = true)
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var services = AppComposition.CreateServices(root);
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(TestContext.Current.CancellationToken);
            var configurations = services.GetRequiredService<IConfigurationRepository>();
            var provider = new AiProvider(Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
                new Uri("https://example.test/v1/"), "fixture-key", null, true, true, new Dictionary<string, string>());
            await configurations.SaveProviderAsync(provider, TestContext.Current.CancellationToken);
            var model = includeModel ? CreateModel(provider.Id, "child") : null;
            if (model is not null) await configurations.SaveModelAsync(model, TestContext.Current.CancellationToken);
            await action(services, provider, model);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }
}
