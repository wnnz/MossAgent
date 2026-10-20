using System.Net;
using Avalonia.Headless;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PullRequestSourceReadSafetyTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData("source.pr_get", 8, 'b')]
    [InlineData("source.pr_files", 8, 'b')]
    [InlineData("source.pr_files", 7, 'c')]
    [InlineData("source.pr_comments", 7, 'c')]
    [InlineData("source.pr_checks", 7, 'c')]
    public Task ModelCannotReadAnotherPrOrRevision(string tool, int number, char head) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestReviewChatFixture.CreateAsync();
        await fixture.OpenAsync(readOnly: true);
        var before = fixture.Source.Transport.RequestCount;
        SourceReadChatScenario.UseTool(fixture, tool, SourceReadChatScenario.Arguments(tool, number, head));

        await SourceReadChatScenario.SendAsync(fixture, "restricted read probe");

        Assert.Equal(before, fixture.Source.Transport.RequestCount);
        var row = Assert.Single(fixture.Workspace.Messages, message => message.Tool is not null);
        Assert.False(row.Tool!.IsSuccess); Assert.Equal("source_scope_denied", row.Tool.ErrorCode);
        using var output = SourceReadChatScenario.ToolOutput(fixture.Transport.Requests.Last());
        Assert.False(output.RootElement.GetProperty("IsSuccess").GetBoolean());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, output.RootElement.GetProperty("Content").ValueKind);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("source.pr_list")]
    [InlineData("filesystem.read_file")]
    [InlineData("subagent.run")]
    [InlineData("mcp.source.pr_get")]
    public Task UnsolicitedOtherToolsNeverGainSourceOrLocalAccess(string tool) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestReviewChatFixture.CreateAsync();
        await fixture.OpenAsync(readOnly: true);
        var before = fixture.Source.Transport.RequestCount;
        SourceReadChatScenario.UseTool(fixture, tool, SourceReadChatScenario.Arguments(tool));

        await SourceReadChatScenario.SendAsync(fixture, "deny unrelated tools");

        Assert.Equal(before, fixture.Source.Transport.RequestCount);
        Assert.False(Assert.Single(fixture.Workspace.Messages, message => message.Tool is not null).Tool!.IsSuccess);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task RemoteHeadChangeDiscardsReturnedSourceContent() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestReviewChatFixture.CreateAsync();
        await fixture.OpenAsync(readOnly: true);
        fixture.Source.Transport.RespondAsync = async (request, token) =>
        {
            var response = await PullRequestInspectionUiFixture.RespondAsync(request, token);
            var body = await response.Content.ReadAsStringAsync(token); response.Content.Dispose();
            response.Content = new StringContent(body.Replace(new string('b', 40), new string('c', 40), StringComparison.Ordinal));
            return response;
        };
        SourceReadChatScenario.UseTool(fixture, "source.pr_get", SourceReadChatScenario.Arguments("source.pr_get"));

        await SourceReadChatScenario.SendAsync(fixture, "do not use changed source data");

        var row = Assert.Single(fixture.Workspace.Messages, message => message.Tool is not null);
        Assert.Equal("source_revision_changed", row.Tool!.ErrorCode);
        using var output = SourceReadChatScenario.ToolOutput(fixture.Transport.Requests.Last());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, output.RootElement.GetProperty("Content").ValueKind);
        Assert.Contains("刷新 PR", output.RootElement.GetProperty("Summary").GetString(), StringComparison.Ordinal);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task StopCancelsInFlightSourceHttpAndNeverStartsTheNextModelRequest() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestReviewChatFixture.CreateAsync();
        var task = await fixture.OpenAsync(readOnly: true);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Source.Transport.RespondAsync = async (request, token) =>
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("/files", StringComparison.Ordinal))
                return await PullRequestInspectionUiFixture.RespondAsync(request, token);
            started.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { cancelled.TrySetResult(); }
            return new HttpResponseMessage(HttpStatusCode.OK);
        };
        SourceReadChatScenario.UseTool(fixture, "source.pr_files", SourceReadChatScenario.Arguments("source.pr_files"));
        fixture.Workspace.ComposerText = "cancel this explicit source read";
        Assert.True(fixture.Workspace.PrimaryActionCommand.CanExecute(null));
        fixture.Workspace.PrimaryActionCommand.Execute(null);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(20), fixture.Token);

        fixture.Workspace.PrimaryActionCommand.Execute(null);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(20), fixture.Token);
        await fixture.WaitUntilAsync(() => !fixture.Workspace.IsRunning && fixture.Workspace.Catalog.SelectedTask?.Status == AgentTaskStatus.Cancelled);

        Assert.Single(fixture.Transport.Requests);
        Assert.Equal(AgentTaskStatus.Cancelled, (await fixture.Tasks.GetTasksAsync(null, fixture.Token)).Single(item => item.Id == task.Id).Status);
        Assert.DoesNotContain(await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token), message => message.Role == MessageRole.Assistant);
        var sourceRequests = fixture.Source.Transport.RequestCount;
        await fixture.Workspace.LoadAsync();
        Assert.Single(fixture.Transport.Requests);
        fixture.Transport.RespondAsync = null;
        await SourceReadChatScenario.SendAsync(fixture, "explicit continuation without repeating the cancelled read");
        Assert.Equal(2, fixture.Transport.Requests.Count);
        Assert.Equal(sourceRequests, fixture.Source.Transport.RequestCount);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("source.pr_files")]
    [InlineData("source.pr_comments")]
    [InlineData("source.pr_checks")]
    public Task RevisionChangeAfterContentHttpDiscardsThePage(string tool) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestReviewChatFixture.CreateAsync();
        var task = await fixture.OpenAsync(readOnly: true);
        var summaries = 0;
        const string marker = "discard-this-unverified-page";
        fixture.Source.Transport.RespondAsync = async (request, token) =>
        {
            var response = await PullRequestInspectionUiFixture.RespondAsync(request, token);
            var body = await response.Content.ReadAsStringAsync(token); response.Content.Dispose();
            if (request.RequestUri!.AbsolutePath.EndsWith("/pulls/7", StringComparison.Ordinal))
            {
                if (++summaries == 2) body = body.Replace(new string('b', 40), new string('c', 40), StringComparison.Ordinal);
            }
            else body = body.Replace("new", marker, StringComparison.Ordinal).Replace("Untrusted comment", marker, StringComparison.Ordinal)
                .Replace("Build", marker, StringComparison.Ordinal);
            response.Content = new StringContent(body);
            return response;
        };
        SourceReadChatScenario.UseTool(fixture, tool, SourceReadChatScenario.Arguments(tool));

        await SourceReadChatScenario.SendAsync(fixture, "check the bound revision after content is received");

        Assert.Equal(2, summaries);
        Assert.Equal("source_revision_changed", Assert.Single(fixture.Workspace.Messages, message => message.Tool is not null).Tool!.ErrorCode);
        using var output = SourceReadChatScenario.ToolOutput(fixture.Transport.Requests.Last());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, output.RootElement.GetProperty("Content").ValueKind);
        Assert.Contains("刷新 PR", output.RootElement.GetProperty("Summary").GetString(), StringComparison.Ordinal);
        Assert.All(fixture.Transport.Requests, request => Assert.DoesNotContain(marker, request, StringComparison.Ordinal));
        Assert.DoesNotContain(await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token), message => message.Content.Contains(marker, StringComparison.Ordinal));
    }, TestContext.Current.CancellationToken);
}
