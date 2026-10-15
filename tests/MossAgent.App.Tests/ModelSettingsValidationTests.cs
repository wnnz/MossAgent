using Microsoft.Extensions.DependencyInjection;
using MossAgent.App;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ModelSettingsValidationTests
{
    [Theory]
    [InlineData(0, 8_192, "medium", "上下文长度")]
    [InlineData(4_000_001, 8_192, "medium", "上下文长度")]
    [InlineData(128_000, 255, "medium", "最大输出 token 数")]
    [InlineData(128_000, 1_000_001, "medium", "最大输出 token 数")]
    [InlineData(8_192, 8_192, "medium", "必须小于上下文长度")]
    [InlineData(128_000, 8_192, "invalid value", "思考强度")]
    [InlineData(128_000, 8_192, "", "思考强度")]
    public async Task SaveModelAsync_RejectsInvalidGenerationParameters(
        int contextLength,
        int maximumOutputTokens,
        string reasoningEffort,
        string expectedError)
    {
        var fixture = await CreateFixtureAsync();
        try
        {
            await using (fixture.Services)
            {
                fixture.ViewModel.ModelId = "manual-model";
                fixture.ViewModel.ContextLength = contextLength;
                fixture.ViewModel.MaximumOutputTokens = maximumOutputTokens;
                fixture.ViewModel.ReasoningEffort = reasoningEffort;

                await fixture.ViewModel.SaveModelCommand.ExecuteAsync(null);

                Assert.Contains(expectedError, fixture.ViewModel.Status);
                Assert.True(fixture.ViewModel.IsFormOpen);
                Assert.Empty(await fixture.Repository.GetModelsAsync(
                    fixture.Provider.Id, CancellationToken.None));
            }
        }
        finally
        {
            SqliteTestPool.Clear(fixture.Root);
            Directory.Delete(fixture.Root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveModelAsync_AcceptsMinimumContextAndOutputTokenValues()
    {
        var fixture = await CreateFixtureAsync();
        try
        {
            await using (fixture.Services)
            {
                fixture.ViewModel.ModelId = "manual-model";
                fixture.ViewModel.ContextLength = 1_024;
                fixture.ViewModel.MaximumOutputTokens = 256;
                fixture.ViewModel.ReasoningEffort = "none";

                await fixture.ViewModel.SaveModelCommand.ExecuteAsync(null);

                var saved = Assert.Single(await fixture.Repository.GetModelsAsync(
                    fixture.Provider.Id, CancellationToken.None));
                Assert.Equal(1_024, saved.ContextLength);
                Assert.Equal(256, saved.MaximumOutputTokens);
                Assert.Equal("none", saved.ReasoningEffort);
            }
        }
        finally
        {
            SqliteTestPool.Clear(fixture.Root);
            Directory.Delete(fixture.Root, recursive: true);
        }
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses, "{\"input\":[]}")]
    [InlineData(ProviderProtocol.OpenAiResponses, "{\"reasoning\":{\"effort\":\"high\"}}")]
    [InlineData(ProviderProtocol.OpenAiCompatible, "{\"max_tokens\":999999}")]
    [InlineData(ProviderProtocol.AnthropicMessages, "{\"system\":\"replaced\"}")]
    [InlineData(ProviderProtocol.GeminiGenerateContent,
        "{\"generationConfig\":{\"maxOutputTokens\":999999}}")]
    public async Task SaveModelAsync_RejectsAdvancedOverridesOfManagedFields(
        ProviderProtocol protocol, string advancedJson)
    {
        var fixture = await CreateFixtureAsync(protocol);
        try
        {
            await using (fixture.Services)
            {
                fixture.ViewModel.ModelId = "manual-model";
                fixture.ViewModel.AdvancedJson = advancedJson;
                if (protocol == ProviderProtocol.GeminiGenerateContent) fixture.ViewModel.ModelId = "gemini-3.8-flash";
                await fixture.ViewModel.SaveModelCommand.ExecuteAsync(null);

                Assert.Contains("高级参数不能覆盖", fixture.ViewModel.Status);
                Assert.True(fixture.ViewModel.IsFormOpen);
                Assert.Empty(await fixture.Repository.GetModelsAsync(
                    fixture.Provider.Id, CancellationToken.None));
            }
        }
        finally
        {
            SqliteTestPool.Clear(fixture.Root);
            Directory.Delete(fixture.Root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveModelAsync_AllowsAdditionalGeminiGenerationOptions()
    {
        var fixture = await CreateFixtureAsync(ProviderProtocol.GeminiGenerateContent);
        try
        {
            await using (fixture.Services)
            {
                fixture.ViewModel.ModelId = "gemini-3.8-flash";
                fixture.ViewModel.AdvancedJson =
                    "{\"generationConfig\":{\"responseMimeType\":\"application/json\"}}";
                await fixture.ViewModel.SaveModelCommand.ExecuteAsync(null);

                var saved = Assert.Single(await fixture.Repository.GetModelsAsync(
                    fixture.Provider.Id, CancellationToken.None));
                Assert.Contains("responseMimeType", saved.AdvancedParametersJson);
            }
        }
        finally
        {
            SqliteTestPool.Clear(fixture.Root);
            Directory.Delete(fixture.Root, recursive: true);
        }
    }

    [Theory]
    [InlineData(4_096, true)]
    [InlineData(999_999, false)]
    public async Task SaveModelAsync_ValidatesAlternateCompletionLimit(int limit, bool accepted)
    {
        var fixture = await CreateFixtureAsync(ProviderProtocol.OpenAiCompatible);
        try
        {
            await using (fixture.Services)
            {
                fixture.ViewModel.ModelId = "manual-model";
                fixture.ViewModel.AdvancedJson = $"{{\"max_completion_tokens\":{limit}}}";
                await fixture.ViewModel.SaveModelCommand.ExecuteAsync(null);

                var models = await fixture.Repository.GetModelsAsync(
                    fixture.Provider.Id, CancellationToken.None);
                Assert.Equal(accepted ? 1 : 0, models.Count);
                if (!accepted) Assert.Contains("max_completion_tokens", fixture.ViewModel.Status);
            }
        }
        finally
        {
            SqliteTestPool.Clear(fixture.Root);
            Directory.Delete(fixture.Root, recursive: true);
        }
    }

    private static async Task<(
        string Root,
        ServiceProvider Services,
        IConfigurationRepository Repository,
        AiProvider Provider,
        ModelSettingsViewModel ViewModel)> CreateFixtureAsync(
            ProviderProtocol protocol = ProviderProtocol.OpenAiResponses)
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var services = AppComposition.CreateServices(root);
        await services.GetRequiredService<IDatabaseInitializer>()
            .InitializeAsync(CancellationToken.None);
        var repository = services.GetRequiredService<IConfigurationRepository>();
        var provider = new AiProvider(
            Guid.NewGuid(), "validation", protocol,
            new Uri("https://validation.example/v1/"), "test-key", null, false, true,
            new Dictionary<string, string>());
        await repository.SaveProviderAsync(provider, CancellationToken.None);
        var viewModel = services.GetRequiredService<ModelSettingsViewModel>();
        await viewModel.RefreshProvidersCommand.ExecuteAsync(null);
        viewModel.AddModelForProviderCommand.Execute(Assert.Single(viewModel.ProviderGroups));
        return (root, services, repository, provider, viewModel);
    }
}
