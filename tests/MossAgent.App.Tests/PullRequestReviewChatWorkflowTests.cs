using System.Text.Json;
using Avalonia.Headless;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PullRequestReviewChatWorkflowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task OpeningAndReopeningPinsLoadedSnapshotWithoutSendingOrReplacingAnotherDraft() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestReviewChatFixture.CreateAsync();
        fixture.Workspace.ComposerText = "ordinary unsent draft";
        await fixture.Workspace.Drafts.FlushAsync();
        await fixture.Page.Inspection.Files.LoadCommand.ExecuteAsync(null);
        await fixture.Page.Inspection.Inline.LoadCommand.ExecuteAsync(null);
        var requests = fixture.Source.Transport.RequestCount;

        var task = await fixture.OpenAsync();

        Assert.Null(task.ProjectId); Assert.Null(task.WorktreePath); Assert.Equal(ApprovalPolicy.ReadOnly, task.ApprovalPolicy);
        Assert.Empty(fixture.Transport.Requests); Assert.Equal(requests, fixture.Source.Transport.RequestCount);
        Assert.Empty(await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token));
        Assert.Equal("ordinary unsent draft", (await fixture.Drafts.GetAsync(new(null, null), fixture.Token))?.Text);
        var binding = Assert.IsType<SourceReviewChatBinding>(await fixture.Chats.GetAsync(task.Id, fixture.Token));
        Assert.Contains("src/sample.cs", binding.Context, StringComparison.Ordinal);
        Assert.Contains("Untrusted comment", binding.Context, StringComparison.Ordinal);
        using var patch = JsonDocument.Parse(binding.Context.Split('\n').First(line => line.Contains("\"Patch\":", StringComparison.Ordinal)));
        Assert.Equal("@@ -1 +1 @@\n-old\n+new", patch.RootElement.GetProperty("Patch").GetString());
        fixture.Workspace.ComposerText = "review unsent draft";
        await fixture.Workspace.Drafts.FlushAsync();

        await fixture.ReturnToPullRequestAsync();
        var reopened = await fixture.OpenAsync();

        Assert.Equal(task.Id, reopened.Id);
        Assert.Equal(binding.Context, (await fixture.Chats.GetAsync(task.Id, fixture.Token))?.Context);
        Assert.Equal("review unsent draft", fixture.Workspace.ComposerText);
        Assert.Empty(fixture.Transport.Requests);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ManualMultiTurnSendUsesSelectedModelAndUntrustedSnapshotWithoutSourceTools() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestReviewChatFixture.CreateAsync();
        await fixture.Page.Inspection.Files.LoadCommand.ExecuteAsync(null);
        var task = await fixture.OpenAsync();
        var sourceRequests = fixture.Source.Transport.RequestCount;

        await fixture.SendAsync("first review prompt");
        await fixture.SendAsync("second review prompt");

        var requests = fixture.Transport.Requests.ToArray();
        Assert.Equal(2, requests.Length);
        using var first = JsonDocument.Parse(requests[0]);
        var root = first.RootElement;
        Assert.Equal("gpt-5", root.GetProperty("model").GetString());
        Assert.Equal("low", root.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.True(!root.TryGetProperty("tools", out var tools) || tools.GetArrayLength() == 0);
        var input = root.GetProperty("input").EnumerateArray().ToArray();
        Assert.Contains(input, item => item.GetProperty("role").GetString() == "user"
            && item.GetRawText().Contains("Untrusted PR snapshot", StringComparison.Ordinal));
        Assert.All(input.Where(item => item.GetProperty("role").GetString() == "system"),
            item => Assert.DoesNotContain("src/sample.cs", item.GetRawText(), StringComparison.Ordinal));
        Assert.DoesNotContain("synthetic-pr-credential", requests[0], StringComparison.Ordinal);
        Assert.Contains("first review prompt", requests[1], StringComparison.Ordinal);
        Assert.Contains("snapshot reply", requests[1], StringComparison.Ordinal);
        Assert.Contains("second review prompt", requests[1], StringComparison.Ordinal);
        Assert.Equal(sourceRequests, fixture.Source.Transport.RequestCount);
        var messages = await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token);
        Assert.Equal(new[] { "first review prompt", "second review prompt" },
            messages.Where(message => message.Role == MessageRole.User).Select(message => message.Content));
        Assert.Equal(2, messages.Count(message => message.Role == MessageRole.Assistant));
        Assert.DoesNotContain(messages, message => message.Role == MessageRole.Tool);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ChangingHeadStartsAnotherConversationAndRetainsOldHistory() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestReviewChatFixture.CreateAsync();
        var first = await fixture.OpenAsync();
        await fixture.SendAsync("review old head");
        fixture.Source.Transport.RespondAsync = async (request, token) =>
        {
            var response = await PullRequestInspectionUiFixture.RespondAsync(request, token);
            var body = await response.Content.ReadAsStringAsync(token);
            response.Content.Dispose();
            response.Content = new StringContent(body.Replace(new string('b', 40), new string('c', 40), StringComparison.Ordinal));
            return response;
        };
        await fixture.ReturnToPullRequestAsync();

        var second = await fixture.OpenAsync();

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(new string('c', 40), (await fixture.Chats.GetAsync(second.Id, fixture.Token))?.Revision.HeadRevision);
        Assert.Empty(await fixture.Tasks.GetMessagesAsync(second.Id, fixture.Token));
        Assert.Equal(2, (await fixture.Tasks.GetMessagesAsync(first.Id, fixture.Token)).Count);
        Assert.Single(fixture.Transport.Requests);
    }, TestContext.Current.CancellationToken);
}
