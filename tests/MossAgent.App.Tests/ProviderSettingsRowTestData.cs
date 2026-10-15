using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal static class ProviderSettingsRowTestData
{
    public static AiProvider Provider(bool enabled = true) => new(
        Guid.NewGuid(), string.Concat(Enumerable.Repeat("很长的中文提供商名称", 12)),
        ProviderProtocol.OpenAiResponses, new Uri("https://fixture.example.test/" + new string('x', 120) + "/v1/"),
        string.Empty, null, true, enabled, new Dictionary<string, string>());

    public static ModelProfile Model(Guid providerId) => new(
        Guid.NewGuid(), providerId, "fixture-" + new string('x', 150),
        string.Concat(Enumerable.Repeat("很长的中文模型名称", 12)),
        128000, 8192, "medium", null, null, false, false, true, true, "{}");
}
