using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

internal sealed class WorkspaceAgentPickerTestFixture : IDisposable
{
    private WorkspaceAgentPickerTestFixture(ModelPickerProbeRepository modelsRepository,
        TestAgentConfigurationRepository repository, ModelPickerViewModel models,
        WorkspaceAgentPickerViewModel picker, AgentProfile first, AgentProfile second, ModelProfile alternative)
    {
        ModelsRepository = modelsRepository;
        Repository = repository;
        Models = models;
        Picker = picker;
        First = first;
        Second = second;
        Alternative = alternative;
    }

    public ModelPickerProbeRepository ModelsRepository { get; }
    public TestAgentConfigurationRepository Repository { get; }
    public ModelPickerViewModel Models { get; }
    public WorkspaceAgentPickerViewModel Picker { get; }
    public AgentProfile First { get; }
    public AgentProfile Second { get; }
    public ModelProfile Alternative { get; }

    public static async Task<WorkspaceAgentPickerTestFixture> CreateAsync(TimeSpan? timeout = null)
    {
        var (configuration, provider, model) = ModelPickerReloadTests.Profiles();
        var alternative = model with { Id = Guid.NewGuid(), ModelId = "alternative", IsDefault = false };
        configuration.Models.Add(alternative);
        var models = new ModelPickerViewModel(configuration);
        await models.ReloadAsync();
        var repository = new TestAgentConfigurationRepository();
        var first = new AgentProfile(Guid.NewGuid(), "first", "first-prompt", provider.Id, model.Id, "high", true);
        var second = new AgentProfile(Guid.NewGuid(), "second", "second-prompt", provider.Id, alternative.Id, "ultra", true);
        await repository.SaveAsync(first, TestContext.Current.CancellationToken);
        await repository.SaveAsync(second, TestContext.Current.CancellationToken);
        var picker = new WorkspaceAgentPickerViewModel(repository, models, timeout ?? TimeSpan.FromSeconds(30));
        await picker.ReloadAsync(TestContext.Current.CancellationToken);
        return new(configuration, repository, models, picker, first, second, alternative);
    }

    public void Dispose()
    {
        Picker.Dispose();
        Models.Dispose();
    }
}
