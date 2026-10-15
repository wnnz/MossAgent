using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ModelPickerMenuTests
{
    [Fact]
    public async Task QuickMenu_OpensModelListAndRestoresDefaultReasoning()
    {
        var (repository, _, model) = ModelPickerReloadTests.Profiles();
        repository.Models[0] = model with { ReasoningEffort = "high" };
        var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        var item = Assert.IsType<ModelPickerItemViewModel>(picker.SelectedItem);

        Assert.True(picker.IsQuickMenuVisible);
        picker.ShowModelListCommand.Execute(null);
        Assert.True(picker.IsModelListVisible);
        picker.SelectItemCommand.Execute(item);
        Assert.True(picker.IsQuickMenuVisible);
        Assert.Equal("Fixture model", picker.SelectedModelName);
        Assert.Equal("高", picker.ReasoningSliderLabel);

        picker.ReasoningSliderLabel = "Ultra";
        Assert.Equal("Ultra", picker.SelectedReasoning);
        Assert.Equal("ultra", picker.EffectiveReasoningEffort);
        picker.ResetReasoningCommand.Execute(null);
        Assert.Equal("默认", picker.SelectedReasoning);
        Assert.Equal("high", picker.EffectiveReasoningEffort);
        Assert.Equal("高", picker.ReasoningSliderLabel);

        picker.ShowModelListCommand.Execute(null);
        picker.ShowQuickMenuCommand.Execute(null);
        Assert.True(picker.IsQuickMenuVisible);
    }

    [Fact]
    public async Task NoConfiguredModel_OpensEmptyListInsteadOfReasoningControls()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var services = AppComposition.CreateServices(root);
            await services.GetRequiredService<IDatabaseInitializer>()
                .InitializeAsync(TestContext.Current.CancellationToken);
            var picker = services.GetRequiredService<WorkspaceViewModel>().ModelPicker;
            await picker.ReloadAsync();

            Assert.True(picker.HasNoModels);
            Assert.Null(picker.SelectedItem);
            Assert.Equal(string.Empty, picker.ReasoningDisplayName);
            picker.ShowQuickMenuCommand.Execute(null);
            Assert.True(picker.IsModelListVisible);
            Assert.False(picker.IsQuickMenuVisible);
            picker.ShowQuickMenuCommand.Execute(null);
            Assert.True(picker.IsModelListVisible);
            Assert.False(string.IsNullOrWhiteSpace(picker.OpenProviderSettingsLabel));
            var requests = 0;
            picker.ProviderSettingsRequested += () => requests++;
            picker.OpenProviderSettingsCommand.Execute(null);
            Assert.Equal(1, requests);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SearchWithNoMatches_KeepsConfiguredModelsAvailable()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var services = AppComposition.CreateServices(root);
            var cancellationToken = TestContext.Current.CancellationToken;
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(cancellationToken);
            var repository = services.GetRequiredService<IConfigurationRepository>();
            var provider = new AiProvider(Guid.NewGuid(), "Fixture", ProviderProtocol.OpenAiResponses,
                new Uri("https://example.test/v1"), "key", null, true, true,
                new Dictionary<string, string>());
            var model = new ModelProfile(Guid.NewGuid(), provider.Id, "model", "Fixture model",
                16_000, 2_000, "medium", null, null, false, false, true, true, "{}");
            await repository.SaveProviderAsync(provider, cancellationToken);
            await repository.SaveModelAsync(model, cancellationToken);
            var picker = services.GetRequiredService<WorkspaceViewModel>().ModelPicker;
            await picker.ReloadAsync();

            Assert.True(picker.HasModels);
            Assert.False(picker.ShowSearch);
            Assert.False(picker.ShowProviderHeaders);
            picker.SearchQuery = "missing";
            Assert.True(picker.HasModels);
            Assert.True(picker.HasNoMatches);
            Assert.False(picker.HasNoModels);
            picker.SearchQuery = string.Empty;
            Assert.False(picker.HasNoMatches);
            Assert.Single(picker.FilteredGroups);

            var secondProvider = provider with { Id = Guid.NewGuid(), Name = "Second" };
            var secondModel = model with { Id = Guid.NewGuid(), ProviderId = secondProvider.Id };
            await repository.SaveProviderAsync(secondProvider, cancellationToken);
            await repository.SaveModelAsync(secondModel, cancellationToken);
            await picker.ReloadAsync();
            Assert.True(picker.ShowSearch);
            Assert.True(picker.ShowProviderHeaders);
            picker.SearchQuery = "Second";
            Assert.Single(picker.FilteredGroups);
            Assert.Equal("Second", picker.FilteredGroups[0].ProviderName);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Flyout_UsesSeparatePagesAndSharedSlider()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var file = Path.Combine(directory.FullName, "src", "MossAgent.App", "Controls",
            "AppModelPickerMenu.axaml");
        var document = XDocument.Load(file);
        Assert.Contains(document.Descendants(), element =>
            element.Name.LocalName == "Grid"
            && element.Attribute("IsVisible")?.Value == "{Binding IsQuickMenuVisible}");
        Assert.Contains(document.Descendants(), element =>
            element.Name.LocalName == "Grid"
            && element.Attribute("IsVisible")?.Value == "{Binding IsModelListVisible}");
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "AppDiscreteSlider");
        Assert.Contains(document.Descendants(), element =>
            element.Name.LocalName == "AppDiscreteSlider"
            && element.Attribute("IsEnabled")?.Value == "{Binding CanUseSelectedModel}");
        Assert.Contains(document.Descendants(), element =>
            element.Name.LocalName == "AppButton"
            && element.Attribute("Content")?.Value == "{Binding OpenProviderSettingsLabel}"
            && element.Attribute("Command")?.Value == "{Binding OpenProviderSettingsCommand}");
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "AppDiscreteSlider"
            && element.Attribute("StepValues")?.Value == "{Binding ReasoningSteps}");
        Assert.DoesNotContain(document.Descendants(), element => element.Attribute("Classes")?.Value == "reasoning-tick");
        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "AppComboBox");
        Assert.Contains("app-slider", new AppSlider().Classes);
    }
}
