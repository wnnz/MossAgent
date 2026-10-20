using MossAgent.Application.Conversations;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteConversationFollowupAuthorizationTests
{
    [Theory]
    [InlineData("primary")]
    [InlineData("roots")]
    [InlineData("model-disabled")]
    [InlineData("model-name")]
    [InlineData("images")]
    [InlineData("files")]
    [InlineData("provider-disabled")]
    [InlineData("protocol")]
    [InlineData("endpoint")]
    [InlineData("key")]
    [InlineData("headers")]
    [InlineData("proxy")]
    [InlineData("agent-disabled")]
    [InlineData("agent-removed")]
    public async Task ChangedAuthorizationRollsBackClaimMessageAndStatus(string kind)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteConversationFollowupRepository(fixture.Connections);
        var item = ConversationFollowupTestData.Create(fixture.Task);
        item = item with { Model = item.Model with { SupportsImages = true, SupportsFiles = true } };
        Assert.True(await repository.SaveAsync(new(item.TaskId, 0, [item]), [], fixture.Token));
        var prepared = await ConversationFollowupDispatchTestData.PrepareAsync(fixture, fixture.Task, item);
        var drafts = new SqliteConversationDraftRepository(fixture.Connections);
        var scope = new ConversationDraftScope(item.ProjectId, item.TaskId);
        Assert.True(await drafts.SaveAsync([new(scope, "keep-next-draft", 0)], fixture.Token));
        await ChangeAsync(fixture, prepared, kind);

        var failure = await Assert.ThrowsAsync<ConversationFollowupException>(() => repository.DispatchAsync(prepared, fixture.Token));

        Assert.Equal(kind is "primary" or "roots" ? "FollowupScopeChanged"
            : kind.StartsWith("agent", StringComparison.Ordinal) ? "FollowupAgentUnavailable" : "FollowupModelUnavailable", failure.Code);
        Assert.Equal(fixture.Task, Assert.Single(await fixture.Repository.GetTasksAsync(item.ProjectId, fixture.Token)));
        Assert.Empty(await fixture.Repository.GetMessagesAsync(item.TaskId, fixture.Token));
        var queue = await repository.GetAsync(item.TaskId, fixture.Token);
        Assert.Equal(1, queue.Revision);
        Assert.Equal(item.Id, Assert.Single(queue.Items).Id);
        Assert.Equal(new("keep-next-draft", 1), await drafts.GetAsync(scope, fixture.Token));
    }

    [Fact]
    public async Task DisplayAndDefaultParameterEditsPreserveTheQueuedSelection()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteConversationFollowupRepository(fixture.Connections);
        var item = ConversationFollowupTestData.Create(fixture.Task);
        Assert.True(await repository.SaveAsync(new(item.TaskId, 0, [item]), [], fixture.Token));
        var prepared = await ConversationFollowupDispatchTestData.PrepareAsync(fixture, fixture.Task, item);
        var configurations = new SqliteConfigurationRepository(fixture.Connections);
        await configurations.SaveProviderAsync(prepared.Provider with { Name = "renamed provider", IsDefault = true }, fixture.Token);
        await configurations.SaveModelAsync(item.Model with { DisplayName = "renamed model", ContextLength = 1024,
            MaximumOutputTokens = 128, ReasoningEffort = "low", Temperature = 0.9 }, fixture.Token);
        await fixture.Repository.SaveProjectAsync(prepared.Project! with { Name = "renamed project" }, fixture.Token);

        var dispatched = Assert.IsType<ConversationFollowupDispatch>(await repository.DispatchAsync(prepared, fixture.Token));

        Assert.Equal(item.Model, dispatched.Followup.Model);
        Assert.Equal(item.AgentInstructions, dispatched.Followup.AgentInstructions);
        Assert.Equal(item.ApprovalPolicy, dispatched.Task.ApprovalPolicy);
        Assert.Empty((await repository.GetAsync(item.TaskId, fixture.Token)).Items);
        Assert.Single(await fixture.Repository.GetMessagesAsync(item.TaskId, fixture.Token));
    }

    private static async Task ChangeAsync(SqliteFailurePersistenceFixture fixture, PreparedConversationFollowup prepared, string kind)
    {
        var configurations = new SqliteConfigurationRepository(fixture.Connections);
        var model = prepared.Model;
        if (kind is "model-disabled" or "model-name" or "images" or "files")
        {
            await configurations.SaveModelAsync(kind switch
            {
                "model-disabled" => model with { IsEnabled = false },
                "model-name" => model with { ModelId = "replacement" },
                "images" => model with { SupportsImages = false },
                _ => model with { SupportsFiles = false }
            }, fixture.Token);
            return;
        }
        if (kind is "primary" or "roots")
        {
            var project = prepared.Project!;
            await fixture.Repository.SaveProjectAsync(kind == "primary" ? project with { PrimaryDirectory = "D:\\changed" }
                : project with { AuthorizedDirectories = ["D:\\changed"] }, fixture.Token);
            return;
        }
        if (kind.StartsWith("agent", StringComparison.Ordinal))
        {
            var agents = new SqliteAgentConfigurationRepository(fixture.Connections);
            var agent = Assert.Single(await agents.GetAllAsync(fixture.Token));
            if (kind == "agent-disabled") await agents.SaveAsync(agent with { IsEnabled = false }, fixture.Token);
            else await agents.DeleteAsync(agent.Id, fixture.Token);
            return;
        }
        var provider = prepared.Provider;
        Guid? proxyId = null;
        if (kind == "proxy")
        {
            proxyId = Guid.NewGuid();
            await configurations.SaveProxyAsync(new(proxyId.Value, "synthetic", ProxyProtocol.Http,
                "127.0.0.1", 1, null, null, false, true), fixture.Token);
        }
        await configurations.SaveProviderAsync(kind switch
        {
            "provider-disabled" => provider with { IsEnabled = false },
            "protocol" => provider with { Protocol = ProviderProtocol.AnthropicMessages },
            "endpoint" => provider with { BaseUri = new("http://127.0.0.1/changed/") },
            "key" => provider with { ApiKey = "synthetic-replacement" },
            "proxy" => provider with { ProxyId = proxyId },
            _ => provider with { Headers = new Dictionary<string, string> { ["x-synthetic"] = "changed" } }
        }, fixture.Token);
    }
}
