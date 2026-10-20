using MossAgent.Application.Conversations;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteConversationSteeringAuthorizationTests
{
    [Theory]
    [InlineData("open", "roots")]
    [InlineData("open", "primary")]
    [InlineData("open", "removed")]
    [InlineData("accept", "roots")]
    [InlineData("accept", "primary")]
    [InlineData("accept", "removed")]
    [InlineData("apply", "roots")]
    [InlineData("apply", "primary")]
    [InlineData("apply", "removed")]
    public async Task ChangedProjectCannotCommitAProtectedOpenAcceptanceOrBoundary(string phase, string mutation)
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var repository = fixture.Repository;
        var project = Assert.Single(await fixture.Workspaces.GetProjectsAsync(fixture.Token));
        var authorization = new ConversationSteeringAuthorization(project, null);
        var run = phase == "open" ? null : Assert.IsType<ConversationSteeringRun>(
            await repository.OpenAsync(fixture.Task, Guid.NewGuid(), fixture.Token, authorization));
        ConversationSteeringAcceptance? accepted = null;
        if (phase == "apply") accepted = Assert.IsType<ConversationSteeringAcceptance>(await repository.AcceptAsync(
            run!.TaskId, run.Id, Guid.NewGuid(), "durable pending guidance", null, fixture.Token, authorization));
        await fixture.ExecuteAsync(mutation switch
        {
            "roots" => "UPDATE projects SET authorized_directories_json='[]';",
            "primary" => "UPDATE projects SET primary_directory='changed-primary';",
            _ => "DELETE FROM projects;"
        });
        var failure = await Assert.ThrowsAsync<ConversationSteeringException>(async () =>
        {
            if (phase == "open") await repository.OpenAsync(fixture.Task, Guid.NewGuid(), fixture.Token, authorization);
            else if (phase == "accept") await repository.AcceptAsync(run!.TaskId, run.Id,
                Guid.NewGuid(), "rejected guidance", null, fixture.Token, authorization);
            else await repository.ApplyAsync(run! with { Revision = accepted!.Revision }, [accepted.Input.Id],
                new(Guid.NewGuid(), run!.TaskId, MessageRole.Assistant, "preceding assistant", DateTimeOffset.UtcNow), fixture.Token, authorization);
        });
        Assert.Equal("SteeringScopeChanged", failure.Code);
        var inputs = (await fixture.InputsAsync()).Items;
        if (phase == "apply" && mutation != "removed") Assert.Equal(ConversationSteeringInputState.Pending, Assert.Single(inputs).State);
        else Assert.Empty(inputs);
        Assert.Empty(await fixture.Workspaces.GetMessagesAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(phase == "open" || mutation == "removed" ? 0 : 1,
            await fixture.CountAsync("SELECT COUNT(*) FROM conversation_steering_runs;"));
    }
}
