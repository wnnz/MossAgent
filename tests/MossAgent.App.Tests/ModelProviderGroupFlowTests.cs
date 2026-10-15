using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ModelProviderGroupFlowTests
{
    [Fact]
    public async Task ProviderGroups_KeepModelsAndEditsScopedToTheirProvider()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var token = TestContext.Current.CancellationToken;
        try
        {
            await using var services = AppComposition.CreateServices(root);
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(token);
            var repository = services.GetRequiredService<IConfigurationRepository>();
            var firstProvider = CreateProvider("first");
            var secondProvider = CreateProvider("second");
            var firstModel = CreateModel(firstProvider.Id, "first-model");
            var secondModel = CreateModel(secondProvider.Id, "second-model");
            await repository.SaveProviderAsync(firstProvider, token);
            await repository.SaveProviderAsync(secondProvider, token);
            await repository.SaveModelAsync(firstModel, token);
            await repository.SaveModelAsync(secondModel, token);

            var viewModel = services.GetRequiredService<ModelSettingsViewModel>();
            await viewModel.RefreshProvidersCommand.ExecuteAsync(null);
            var firstGroup = Assert.Single(viewModel.ProviderGroups, group => group.Provider.Id == firstProvider.Id);
            var secondGroup = Assert.Single(viewModel.ProviderGroups, group => group.Provider.Id == secondProvider.Id);
            Assert.Equal("first-model", Assert.Single(firstGroup.Models).ModelId);
            Assert.Equal("second-model", Assert.Single(secondGroup.Models).ModelId);

            viewModel.ToggleProviderCommand.Execute(secondGroup);
            Assert.True(secondGroup.IsExpanded);
            viewModel.AddModelForProviderCommand.Execute(secondGroup);
            Assert.Equal(secondProvider.Id, viewModel.SelectedProvider?.Id);
            viewModel.ModelId = "manual-model";
            viewModel.DisplayName = "Manual model";
            viewModel.ContextLength = 256_000;
            viewModel.ReasoningEffort = "high";
            await viewModel.SaveModelCommand.ExecuteAsync(null);
            var updatedSecondGroup = Assert.Single(viewModel.ProviderGroups, group => group.Provider.Id == secondProvider.Id);
            Assert.Contains(updatedSecondGroup.Models, model => model.ModelId == "manual-model");
            Assert.Single(Assert.Single(viewModel.ProviderGroups, group => group.Provider.Id == firstProvider.Id).Models);

            viewModel.EditProviderModelCommand.Execute(secondModel);
            viewModel.ContextLength = 512_000;
            await viewModel.SaveModelCommand.ExecuteAsync(null);
            var savedModel = Assert.Single(await repository.GetModelsAsync(secondProvider.Id, token), model => model.Id == secondModel.Id);
            Assert.Equal(512_000, savedModel.ContextLength);
            await AssertProviderProxyBindingAsync(services, viewModel, repository, token);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task AssertProviderProxyBindingAsync(ServiceProvider services,
        ModelSettingsViewModel models, IConfigurationRepository repository, CancellationToken token)
    {
        var proxySettings = services.GetRequiredService<ProxySettingsViewModel>();
        await proxySettings.RefreshCommand.ExecuteAsync(null);
        proxySettings.OpenCreateFormCommand.Execute(null);
        proxySettings.Name = "proxy-one";
        proxySettings.Host = "127.0.0.1";
        proxySettings.Port = 7890;
        await proxySettings.SaveCommand.ExecuteAsync(null);
        proxySettings.OpenCreateFormCommand.Execute(null);
        proxySettings.Name = "proxy-two";
        proxySettings.Host = "127.0.0.1";
        proxySettings.Port = 7891;
        await proxySettings.SaveCommand.ExecuteAsync(null);
        Assert.Equal(2, proxySettings.Items.Count);

        var providerSettings = services.GetRequiredService<ProviderSettingsViewModel>();
        await providerSettings.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(3, providerSettings.Proxies.Count);
        providerSettings.OpenCreateFormCommand.Execute(null);
        providerSettings.Name = "third";
        providerSettings.BaseUrl = "https://third.example/v1/";
        providerSettings.SelectedProxy = Assert.Single(providerSettings.Proxies, proxy => proxy.Name == "proxy-two");
        var selectedProxyId = providerSettings.SelectedProxy!.Id;
        await providerSettings.SaveCommand.ExecuteAsync(null);
        Assert.Contains(models.ProviderGroups, group => group.Provider.Name == "third");
        var savedProvider = Assert.Single(await repository.GetProvidersAsync(token), provider => provider.Name == "third");
        Assert.Equal(selectedProxyId, savedProvider.ProxyId);
    }

    private static AiProvider CreateProvider(string name) =>
        new(Guid.NewGuid(), name, ProviderProtocol.OpenAiResponses,
            new Uri($"https://{name}.example/v1/"), "test-key", null, false, true,
            new Dictionary<string, string>());

    private static ModelProfile CreateModel(Guid providerId, string id) =>
        new(Guid.NewGuid(), providerId, id, id, 128_000, 8_192, "medium",
            null, null, false, false, false, true, "{}");
}
