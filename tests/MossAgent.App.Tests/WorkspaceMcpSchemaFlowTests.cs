using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Mcp;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceMcpSchemaFlowTests
{
    [Theory]
    [InlineData("{\"message\":\"safe\",\"options\":{\"mode\":\"safe\"},\"extra\":true}")]
    [InlineData("{\"message\":\"safe\",\"options\":{\"mode\":\"safe\",\"extra\":true}}")]
    [InlineData("{\"message\":\"safe\",\"options\":{\"mode\":\"synthetic-secret\"}}")]
    public async Task InvalidArguments_AreVisibleAndPersistedWithoutApprovalRemoteCallOrReplay(string arguments)
    {
        var schema = JsonSerializer.Deserialize<JsonElement>("""
            {"type":"object","required":["message","options"],
             "properties":{"message":{"type":"string"},"options":{"$ref":"#/$defs/options"}},
             "$defs":{"options":{"type":"object","required":["mode"],"properties":{"mode":{"const":"safe"}},
             "additionalProperties":false}},"additionalProperties":false}
            """);
        await using var fixture = await WorkspaceMcpFixture.CreateAsync("unused",
            transport: McpTransportKind.StreamableHttp, arguments: JsonSerializer.Deserialize<JsonElement>(arguments), schema: schema);
        var prompts = 0;
        System.ComponentModel.PropertyChangedEventHandler handler = (_, change) =>
        {
            if (change.PropertyName == nameof(fixture.Approval.IsPending) && fixture.Approval.IsPending) prompts++;
        };
        fixture.Approval.PropertyChanged += handler;
        try
        {
            fixture.Start();
            await fixture.WaitForCompletionAsync();
        }
        finally { fixture.Approval.PropertyChanged -= handler; }

        Assert.Equal(0, prompts);
        Assert.False(fixture.HttpServer.CallStarted.Task.IsCompleted);
        var result = await fixture.AssertPersistedAsync("invalid_arguments", null);
        Assert.DoesNotContain("synthetic-secret", result.Summary + result.Content, StringComparison.Ordinal);
        Assert.Equal(2, fixture.Model.Requests.Count);
        using var followup = JsonDocument.Parse(fixture.Model.Requests[1]);
        var output = Assert.Single(followup.RootElement.GetProperty("input").EnumerateArray(), item =>
            item.TryGetProperty("type", out var type) && type.GetString() == "function_call_output");
        using var observation = JsonDocument.Parse(output.GetProperty("output").GetString()!);
        Assert.False(observation.RootElement.GetProperty("IsSuccess").GetBoolean());
        Assert.Equal(result.Summary, observation.RootElement.GetProperty("Summary").GetString());
        var summaries = fixture.Workspace.Messages.Where(message => message.IsTool).Select(message => message.Tool!.Summary).ToArray();

        await fixture.ReopenAsync();

        Assert.Equal(AgentTaskStatus.Completed, fixture.Workspace.Catalog.SelectedTask!.Status);
        Assert.False(fixture.Workspace.IsRunning);
        Assert.Equal(summaries, fixture.Workspace.Messages.Where(message => message.IsTool).Select(message => message.Tool!.Summary));
        Assert.False(fixture.HttpServer.CallStarted.Task.IsCompleted);
        Assert.Equal(2, fixture.Model.Requests.Count);
        await fixture.AssertPersistedAsync("invalid_arguments", null);
    }
}
