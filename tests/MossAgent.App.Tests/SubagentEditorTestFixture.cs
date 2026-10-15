using MossAgent.App.ViewModels;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class SubagentEditorTestFixture : IDisposable
{
    public SubagentEditorTestFixture(TimeSpan? operationTimeout = null)
    {
        var provider = new AiProvider(Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
            new Uri("https://example.test/v1"), "fixture-key", null, false, true,
            new Dictionary<string, string>());
        Model = new ModelProfile(Guid.NewGuid(), provider.Id, "child", "child", 16000, 2000,
            "medium", null, null, false, false, false, true, "{}");
        Configurations = new TestSubagentModelConfigurationRepository(provider, Model);
        Settings = new SubagentSettingsViewModel(Repository, Configurations, operationTimeout)
        {
            Name = "original",
            TriggerCondition = "original trigger",
            SystemPrompt = "original prompt"
        };
    }

    public TestSubagentConfigurationRepository Repository { get; } = new();
    public TestSubagentModelConfigurationRepository Configurations { get; }
    public ModelProfile Model { get; }
    public SubagentSettingsViewModel Settings { get; }
    public void Dispose() => Settings.Dispose();
}
