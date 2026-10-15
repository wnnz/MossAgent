using Microsoft.Extensions.DependencyInjection;
using MossAgent.App;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

/// <summary>
/// 模型选择器视图模型单元测试，验证搜索过滤、分组展示及模型选中事件。
/// </summary>
public sealed class ModelPickerViewModelTests
{
    [Fact]
    public async Task ModelPicker_FilterAndSelect_WorksCorrectly()
    {
        var (repository, testProvider, testModel) = ModelPickerReloadTests.Profiles();
        var picker = new ModelPickerViewModel(repository);

        Assert.NotNull(picker);
        Assert.NotNull(picker.FilteredGroups);
        Assert.NotEmpty(picker.ReasoningSteps);
        Assert.Equal("默认", picker.SelectedReasoning);

        testProvider = testProvider with { Name = "TestProvider" };
        testModel = testModel with { DisplayName = "GPT Test" };
        repository.Providers[0] = testProvider;
        repository.Models[0] = testModel;
        await picker.ReloadAsync();

        AiProvider? selectedProvider = null;
        ModelProfile? selectedModel = null;
        picker.ModelSelected += (p, m) =>
        {
            selectedProvider = p;
            selectedModel = m;
        };

        var item = Assert.IsType<ModelPickerItemViewModel>(picker.SelectedItem);
        Assert.Equal("GPT Test · TestProvider", item.AccessibleName);
        picker.SelectItemCommand.Execute(item);

        Assert.Same(testProvider, selectedProvider);
        Assert.Same(testModel, selectedModel);
        Assert.Same(item, picker.SelectedItem);
        Assert.Contains("GPT Test", picker.CurrentDisplayName);
        Assert.Equal("medium", picker.EffectiveReasoningEffort);
        Assert.Equal("中", picker.ReasoningDisplayName);

        picker.SelectedReasoning = "高";
        Assert.Equal("high", picker.EffectiveReasoningEffort);
        Assert.Equal("高", picker.ReasoningDisplayName);

        picker.SelectedReasoning = "默认";
        repository.Models[0] = testModel with { ReasoningEffort = "xhigh" };
        await picker.ReloadAsync();
        picker.SelectItemCommand.Execute(item);
        Assert.Equal("最高", picker.ReasoningDisplayName);
    }

    [Fact]
    public async Task ReloadAsync_WhenAllModelsAreDisabled_ClearsCurrentSelection()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var services = AppComposition.CreateServices(root);
            await services.GetRequiredService<IDatabaseInitializer>()
                .InitializeAsync(CancellationToken.None);
            var repository = services.GetRequiredService<IConfigurationRepository>();
            var provider = new AiProvider(
                Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
                new Uri("https://example.test/v1"), "test-key", null, true, true,
                new Dictionary<string, string>());
            var model = new ModelProfile(
                Guid.NewGuid(), provider.Id, "fixture-model", "Fixture model",
                16_000, 2_000, "medium", null, null, false, false, false, true, "{}");
            await repository.SaveProviderAsync(provider, CancellationToken.None);
            await repository.SaveModelAsync(model, CancellationToken.None);

            var workspace = services.GetRequiredService<WorkspaceViewModel>();
            var picker = workspace.ModelPicker;
            AiProvider? selectedProvider = null;
            ModelProfile? selectedModel = null;
            picker.ModelSelected += (providerSelection, modelSelection) =>
            {
                selectedProvider = providerSelection;
                selectedModel = modelSelection;
            };

            await picker.ReloadAsync();
            Assert.Equal(model.Id, selectedModel?.Id);
            Assert.Equal(model.Id, workspace.SelectedModel?.Id);
            await repository.SaveModelAsync(model with { IsEnabled = false }, CancellationToken.None);

            await picker.ReloadAsync();

            Assert.Null(picker.SelectedItem);
            Assert.Null(selectedProvider);
            Assert.Null(selectedModel);
            Assert.Null(workspace.SelectedProvider);
            Assert.Null(workspace.SelectedModel);
            Assert.Equal("默认", picker.SelectedReasoning);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DisablingSelectedModel_RefreshesWorkspaceWithoutManualPickerReload()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var services = AppComposition.CreateServices(root);
            await services.GetRequiredService<IDatabaseInitializer>()
                .InitializeAsync(CancellationToken.None);
            var repository = services.GetRequiredService<IConfigurationRepository>();
            var provider = new AiProvider(
                Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
                new Uri("https://example.test/v1"), "test-key", null, true, true,
                new Dictionary<string, string>());
            var model = new ModelProfile(
                Guid.NewGuid(), provider.Id, "fixture-model", "Fixture model",
                16_000, 2_000, "medium", null, null, false, false, true, true, "{}");
            await repository.SaveProviderAsync(provider, CancellationToken.None);
            await repository.SaveModelAsync(model, CancellationToken.None);

            var mainWindow = services.GetRequiredService<MainWindowViewModel>();
            await mainWindow.Models.RefreshProvidersCommand.ExecuteAsync(null);
            await mainWindow.Workspace.ModelPicker.ReloadAsync();
            Assert.Equal(model.Id, mainWindow.Workspace.SelectedModel?.Id);

            mainWindow.Models.EditProviderModelCommand.Execute(model);
            mainWindow.Models.IsEnabled = false;
            await mainWindow.Models.SaveModelCommand.ExecuteAsync(null);

            Assert.Null(mainWindow.Workspace.SelectedProvider);
            Assert.Null(mainWindow.Workspace.SelectedModel);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }
}
