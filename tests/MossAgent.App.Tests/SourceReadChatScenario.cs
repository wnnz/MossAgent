using System.Net;
using System.Text;
using System.Text.Json;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

internal static class SourceReadChatScenario
{
    public static JsonElement Arguments(string tool, int number = 7, char head = 'b', string? kind = null)
    {
        var values = new Dictionary<string, object> { ["number"] = number };
        if (tool != "source.pr_get" && tool != "source.pr_list")
        {
            values["base_revision"] = new string('a', 40); values["head_revision"] = new string(head, 40); values["page"] = 1;
            if (tool is "source.pr_comments" or "source.pr_checks") values["kind"] = kind ?? (tool == "source.pr_comments" ? "inline" : "runs");
        }
        if (tool == "source.pr_list") { values.Clear(); values["page"] = 1; }
        return JsonSerializer.SerializeToElement(values);
    }

    public static void UseTool(PullRequestReviewChatFixture fixture, string tool, JsonElement arguments)
    {
        var calls = 0;
        fixture.Transport.RespondAsync = _ =>
        {
            if (Interlocked.Increment(ref calls) != 1) return Task.FromResult(ReviewChatModelTransport.Reply());
            using var request = JsonDocument.Parse(fixture.Transport.Requests.Last());
            var prefix = tool.Replace('.', '_') + "__";
            var wireName = request.RootElement.GetProperty("tools").EnumerateArray().Select(item => item.GetProperty("name").GetString()!)
                .FirstOrDefault(name => name.StartsWith(prefix, StringComparison.Ordinal)) ?? tool;
            var frame = JsonSerializer.Serialize(new { type = "response.function_call_arguments.done", name = wireName, call_id = "source-read", arguments = arguments.GetRawText() });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("data: " + frame + "\n\n"
                + "data: {\"type\":\"response.completed\",\"response\":{\"id\":\"fixture-response\",\"status\":\"completed\"}}\n\n"
                + "data: [DONE]\n\n", Encoding.UTF8, "text/event-stream") });
        };
    }

    public static async Task SendAsync(PullRequestReviewChatFixture fixture, string prompt)
    {
        var count = fixture.Transport.Requests.Count;
        fixture.Workspace.ComposerText = prompt;
        Assert.True(fixture.Workspace.PrimaryActionCommand.CanExecute(null));
        fixture.Workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Transport.Requests.Count > count && !fixture.Workspace.IsRunning
            && fixture.Workspace.Catalog.SelectedTask?.Status == AgentTaskStatus.Completed);
    }

    public static JsonDocument ToolOutput(string request)
    {
        using var document = JsonDocument.Parse(request);
        var output = Assert.Single(document.RootElement.GetProperty("input").EnumerateArray(),
            item => item.TryGetProperty("type", out var type) && type.GetString() == "function_call_output");
        return JsonDocument.Parse(output.GetProperty("output").GetString()!);
    }
}
