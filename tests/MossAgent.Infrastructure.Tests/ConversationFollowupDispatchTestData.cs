using MossAgent.Application.Conversations;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;

namespace MossAgent.Infrastructure.Tests;

internal static class ConversationFollowupDispatchTestData
{
    public static async Task<PreparedConversationFollowup> PrepareAsync(SqliteFailurePersistenceFixture fixture,
        AgentTask task, ConversationFollowup item, long revision = 1)
    {
        // These are storage-only tests: no path is opened and no provider is invoked.
        var project = task.ProjectId is null ? null : fixture.Project with
        { PrimaryDirectory = item.AuthorizedRoots[0], AuthorizedDirectories = item.AuthorizedRoots.ToArray() };
        if (project is not null)
        {
            await fixture.Repository.SaveProjectAsync(project, fixture.Token);
            project = await fixture.Repository.ReadAuthorizationAsync(project.Id, fixture.Token);
        }
        var provider = new AiProvider(item.Model.ProviderId, "synthetic", ProviderProtocol.OpenAiCompatible,
            new Uri("http://127.0.0.1/"), "synthetic-key", null, false, true, new Dictionary<string, string>());
        var configurations = new SqliteConfigurationRepository(fixture.Connections);
        await configurations.SaveProviderAsync(provider, fixture.Token);
        await configurations.SaveModelAsync(item.Model, fixture.Token);
        if (item.AgentProfileId is { } id)
            await new SqliteAgentConfigurationRepository(fixture.Connections).SaveAsync(new AgentProfile(id, "synthetic",
                item.AgentInstructions ?? "", provider.Id, item.Model.Id, item.Model.ReasoningEffort, true), fixture.Token);
        return new(task, project, provider, item.Model, item, revision);
    }
}
