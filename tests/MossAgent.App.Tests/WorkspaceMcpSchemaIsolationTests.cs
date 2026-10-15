using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Mcp;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceMcpSchemaIsolationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConcurrentTasks_WithSameSchemaIdKeepResultsAndRestoreWithoutReplay(bool secondAccepted)
    {
        var identifier = $"https://example.invalid/schemas/{Guid.NewGuid():N}";
        await using var first = await WorkspaceMcpFixture.CreateAsync("first-task",
            transport: McpTransportKind.StreamableHttp, schema: Schema(identifier, "first-task"));
        await using var second = await WorkspaceMcpFixture.CreateAsync(secondAccepted ? "second-task" : "first-task",
            transport: McpTransportKind.StreamableHttp, schema: Schema(identifier, "second-task"));

        first.Start(ApprovalPolicy.FullAccess);
        second.Start(ApprovalPolicy.FullAccess);
        await Task.WhenAll(first.WaitForCompletionAsync(), second.WaitForCompletionAsync());

        Assert.NotEqual(first.TaskId, second.TaskId);
        await AssertResultAndRestoreAsync(first, "first-task", true);
        await AssertResultAndRestoreAsync(second, "second-task", secondAccepted);
    }

    private static JsonElement Schema(string identifier, string expected) => JsonSerializer.SerializeToElement(
        new Dictionary<string, object>
        {
            ["$id"] = identifier,
            ["type"] = "object",
            ["properties"] = new { message = new Dictionary<string, string> { ["$ref"] = "#/$defs/message" } },
            ["$defs"] = new { message = new { type = "string", @const = expected } },
            ["required"] = new[] { "message" },
            ["additionalProperties"] = false
        });

    private static async Task AssertResultAndRestoreAsync(WorkspaceMcpFixture fixture, string expected, bool accepted)
    {
        var error = accepted ? null : "invalid_arguments";
        var result = await fixture.AssertPersistedAsync(error, accepted ? true : null);
        if (accepted) Assert.Equal(expected, result.Content);
        Assert.Equal(accepted, fixture.HttpServer.CallStarted.Task.IsCompleted);
        Assert.Equal(2, fixture.Model.Requests.Count);

        await fixture.ReopenAsync();

        Assert.Equal(AgentTaskStatus.Completed, fixture.Workspace.Catalog.SelectedTask!.Status);
        Assert.False(fixture.Workspace.IsRunning);
        Assert.Equal(2, fixture.Model.Requests.Count);
        Assert.Equal(accepted, fixture.HttpServer.CallStarted.Task.IsCompleted);
        var restored = await fixture.AssertPersistedAsync(error, accepted ? true : null);
        Assert.Equal(result, restored);
    }
}
