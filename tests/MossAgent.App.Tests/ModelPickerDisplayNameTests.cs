using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ModelPickerDisplayNameTests
{
    [Fact]
    public async Task EmptyCatalog_KeepsTheSelectionPromptWithoutProviderText()
    {
        using var picker = new ModelPickerViewModel(new ModelPickerProbeRepository());

        await picker.ReloadAsync();

        Assert.Equal("选择模型...", picker.ComposerDisplayName);
        Assert.Equal(picker.CurrentDisplayName, picker.ComposerDisplayName);
        Assert.True(picker.HasNoModels);
        Assert.False(picker.CanSendWithSelectedModel);
    }

    [Fact]
    public async Task FailedReload_UpdatesBothLabelsAndRestoresTheSameIdentity()
    {
        var (repository, provider, model) = ModelPickerReloadTests.Profiles();
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        var notifications = new List<string?>();
        picker.PropertyChanged += (_, change) => notifications.Add(change.PropertyName);
        repository.ModelLoader = _ => Task.FromException<IReadOnlyList<ModelProfile>>(
            new InvalidOperationException("fixture-secret-error"));

        await picker.ReloadAsync();

        Assert.True(picker.HasLoadError);
        Assert.Equal(picker.CurrentDisplayName, picker.ComposerDisplayName);
        Assert.NotEqual(model.DisplayName, picker.ComposerDisplayName);
        Assert.DoesNotContain("fixture-secret-error", picker.ComposerDisplayName);
        Assert.Contains(nameof(picker.ComposerDisplayName), notifications);
        Assert.Contains(nameof(picker.CurrentDisplayName), notifications);
        Assert.False(picker.CanSendWithSelectedModel);
        notifications.Clear();
        repository.ModelLoader = _ => Task.FromResult<IReadOnlyList<ModelProfile>>([model]);

        await picker.ReloadAsync();

        Assert.Equal(model.Id, picker.SelectedItem?.Model.Id);
        Assert.Equal(model.DisplayName, picker.ComposerDisplayName);
        Assert.Equal($"{model.DisplayName} · {provider.Name}", picker.CurrentDisplayName);
        Assert.Contains(nameof(picker.ComposerDisplayName), notifications);
        Assert.True(picker.CanSendWithSelectedModel);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task InitialSelection_UsesTheDefaultProviderAndItsDefaultModel(bool defaultProvider, bool defaultModel)
    {
        var (repository, provider, model) = ModelPickerReloadTests.Profiles();
        repository.Providers[0] = provider with { IsDefault = false };
        var alternativeProvider = provider with { Id = Guid.NewGuid(), Name = "alternative", IsDefault = defaultProvider };
        var alternative = model with { Id = Guid.NewGuid(), ProviderId = alternativeProvider.Id, IsDefault = false };
        var preferred = alternative with { Id = Guid.NewGuid(), IsDefault = defaultModel };
        repository.Providers.Add(alternativeProvider);
        repository.Models.AddRange([alternative, preferred]);
        using var picker = new ModelPickerViewModel(repository);

        await picker.ReloadAsync();

        var expected = defaultProvider ? defaultModel ? preferred : alternative : model;
        Assert.Equal(expected.Id, picker.SelectedItem?.Model.Id);
        Assert.Equal(expected.DisplayName, picker.ComposerDisplayName);
    }
}
