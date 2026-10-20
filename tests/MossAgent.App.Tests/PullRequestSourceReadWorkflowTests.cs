using System.Text.Json;
using Avalonia.Headless;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PullRequestSourceReadWorkflowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task OpeningSourceModeDoesNotSendReadOrChangeSnapshotChatOrDraft() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestReviewChatFixture.CreateAsync();
        var snapshot = await fixture.OpenAsync();
        fixture.Workspace.ComposerText = "snapshot draft";
        await fixture.Workspace.Drafts.FlushAsync();
        await fixture.ReturnToPullRequestAsync();
        var count = fixture.Source.Transport.RequestCount;

        var connected = await fixture.OpenAsync(readOnly: true);

        Assert.NotEqual(snapshot.Id, connected.Id); Assert.Null(connected.ProjectId); Assert.Null(connected.WorktreePath);
        Assert.Equal(ApprovalPolicy.ReadOnly, connected.ApprovalPolicy);
        Assert.Equal(SourceReviewChatMode.ReadOnlySource, (await fixture.Chats.GetAsync(connected.Id, fixture.Token))!.Mode);
        Assert.Equal(SourceReviewChatMode.Snapshot, (await fixture.Chats.GetAsync(snapshot.Id, fixture.Token))!.Mode);
        Assert.Empty(fixture.Transport.Requests); Assert.Equal(count, fixture.Source.Transport.RequestCount);
        Assert.Equal("snapshot draft", (await fixture.Drafts.GetAsync(new(null, snapshot.Id), fixture.Token))!.Text);
        await fixture.ReturnToPullRequestAsync();
        Assert.Equal(connected.Id, (await fixture.OpenAsync(readOnly: true)).Id);
        await fixture.ReturnToPullRequestAsync();
        Assert.Equal(snapshot.Id, (await fixture.OpenAsync()).Id);
        Assert.Equal("snapshot draft", fixture.Workspace.ComposerText);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("source.pr_get", null, 1)]
    [InlineData("source.pr_files", null, 3)]
    [InlineData("source.pr_comments", "inline", 3)]
    [InlineData("source.pr_comments", "conversation", 3)]
    [InlineData("source.pr_checks", "runs", 3)]
    [InlineData("source.pr_checks", "statuses", 3)]
    public Task ModelToolSourceModelRoundtripIsScopedAuditedVisibleAndRestorable(string tool, string? kind, int sourceRequests) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestReviewChatFixture.CreateAsync();
        var task = await fixture.OpenAsync(readOnly: true);
        var binding = (await fixture.Chats.GetAsync(task.Id, fixture.Token))!;
        var count = fixture.Source.Transport.RequestCount;
        var sourceOnUiThread = false;
        fixture.Source.Transport.RespondAsync = (request, token) =>
        {
            sourceOnUiThread |= Avalonia.Threading.Dispatcher.UIThread.CheckAccess();
            return PullRequestInspectionUiFixture.RespondAsync(request, token);
        };
        SourceReadChatScenario.UseTool(fixture, tool, SourceReadChatScenario.Arguments(tool, kind: kind));

        await SourceReadChatScenario.SendAsync(fixture, "Please query only this PR's requested data.");

        var requests = fixture.Transport.Requests.ToArray();
        Assert.False(sourceOnUiThread, "Source HTTP must not execute on the UI thread.");
        Assert.Equal(2, requests.Length); Assert.Equal(count + sourceRequests, fixture.Source.Transport.RequestCount);
        using var first = JsonDocument.Parse(requests[0]);
        var names = first.RootElement.GetProperty("tools").EnumerateArray().Select(item => item.GetProperty("name").GetString()!).ToArray();
        Assert.Equal(4, names.Length);
        Assert.All(names, name => Assert.StartsWith("source_pr_", name, StringComparison.Ordinal));
        Assert.DoesNotContain(names, name => name.StartsWith("source_pr_list", StringComparison.Ordinal));
        foreach (var body in requests)
        {
            using var document = JsonDocument.Parse(body);
            var pinned = document.RootElement.GetProperty("input").EnumerateArray().Where(item => item.TryGetProperty("role", out var role)
                && role.GetString() == "user" && item.GetProperty("content").ValueKind == JsonValueKind.String
                && item.GetProperty("content").GetString() == binding.Context);
            Assert.Single(pinned);
            Assert.DoesNotContain("synthetic-pr-credential", body, StringComparison.Ordinal);
        }
        using var output = SourceReadChatScenario.ToolOutput(requests[1]);
        Assert.True(output.RootElement.GetProperty("IsSuccess").GetBoolean());
        using var content = JsonDocument.Parse(output.RootElement.GetProperty("Content").GetString()!);
        Assert.Equal(7, (tool == "source.pr_get" ? content.RootElement : content.RootElement.GetProperty("Revision")).GetProperty("Number").GetInt32());
        Assert.True(Assert.Single(fixture.Workspace.Messages, message => message.Tool is not null).Tool!.IsSuccess);
        Assert.Single(await fixture.Source.Audit.GetRecentAsync(task.Id, 10, fixture.Token));
        Assert.Single(await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token), message => message.Role == MessageRole.Tool);

        await fixture.Workspace.LoadAsync();
        Assert.Single(fixture.Workspace.Messages, message => message.Tool is not null);
        await SourceReadChatScenario.SendAsync(fixture, "manual follow-up using the recorded observation");
        Assert.Equal(3, fixture.Transport.Requests.Count);
        Assert.Equal(count + sourceRequests, fixture.Source.Transport.RequestCount);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SwitchingBackToSnapshotChatNeverInheritsSourceToolsOrReadHistory() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestReviewChatFixture.CreateAsync();
        var snapshot = await fixture.OpenAsync();
        await fixture.ReturnToPullRequestAsync();
        var connected = await fixture.OpenAsync(readOnly: true);
        SourceReadChatScenario.UseTool(fixture, "source.pr_files", SourceReadChatScenario.Arguments("source.pr_files"));
        await SourceReadChatScenario.SendAsync(fixture, "source-mode only query");
        Assert.Equal(3, (await fixture.Tasks.GetMessagesAsync(connected.Id, fixture.Token)).Count);
        Assert.Empty(await fixture.Tasks.GetMessagesAsync(snapshot.Id, fixture.Token));
        await fixture.ReturnToPullRequestAsync();
        Assert.Equal(snapshot.Id, (await fixture.OpenAsync()).Id);
        var count = fixture.Source.Transport.RequestCount;
        fixture.Transport.RespondAsync = null;

        await fixture.SendAsync("snapshot-mode follow-up");

        using var request = JsonDocument.Parse(fixture.Transport.Requests.Last());
        Assert.False(request.RootElement.TryGetProperty("tools", out var tools) && tools.GetArrayLength() > 0);
        Assert.DoesNotContain("source-mode only query", fixture.Transport.Requests.Last(), StringComparison.Ordinal);
        Assert.Equal(count, fixture.Source.Transport.RequestCount);
        Assert.Equal(3, (await fixture.Tasks.GetMessagesAsync(connected.Id, fixture.Token)).Count);
        Assert.Equal(2, (await fixture.Tasks.GetMessagesAsync(snapshot.Id, fixture.Token)).Count);
    }, TestContext.Current.CancellationToken);
}
