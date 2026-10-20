using System.Net;
using System.Text.Json;
using Avalonia.Headless;
using MossAgent.App.Resources;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PullRequestReviewChatSafetyTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RevocationOrCredentialChangeBeforeSendKeepsDraftAndCannotPersistAnotherPromptOrCallProvider(bool credential) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestReviewChatFixture.CreateAsync();
        var task = await fixture.OpenAsync();
        var binding = (await fixture.Chats.GetAsync(task.Id, fixture.Token))!;
        if (credential)
        {
            var account = (await fixture.Source.Accounts.GetAsync(binding.Scope.AccountId, fixture.Token))!;
            await fixture.Source.Accounts.SaveAsync(new(account.Id, account.Name, account.ApiEndpoint, "changed-synthetic-token",
                account.ProxyId, account.IsEnabled, account.Revision), fixture.Token);
        }
        else await fixture.Source.Grants.RevokeAsync(binding.Scope.GrantId, fixture.Token);
        fixture.Workspace.ComposerText = "must remain unsent";

        fixture.Workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => !fixture.Workspace.IsRunning && fixture.Workspace.HasSendPreparationError);

        Assert.Equal("must remain unsent", fixture.Workspace.ComposerText);
        Assert.Empty(await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token));
        Assert.Empty(fixture.Transport.Requests);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task RevocationWhileProviderIsRunningPreventsReplyPublication() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestReviewChatFixture.CreateAsync();
        var task = await fixture.OpenAsync();
        var binding = (await fixture.Chats.GetAsync(task.Id, fixture.Token))!;
        fixture.Transport.RespondAsync = async token =>
        {
            await fixture.Source.Grants.RevokeAsync(binding.Scope.GrantId, token);
            return ReviewChatModelTransport.Reply();
        };
        fixture.Workspace.ComposerText = "manual review before revocation";

        Assert.True(fixture.Workspace.PrimaryActionCommand.CanExecute(null));
        fixture.Workspace.PrimaryActionCommand.Execute(null);
        await fixture.Transport.Started.Task.WaitAsync(TimeSpan.FromSeconds(20), fixture.Token);
        await fixture.WaitUntilAsync(() => !fixture.Workspace.IsRunning && fixture.Workspace.Catalog.SelectedTask?.Status == AgentTaskStatus.Failed);

        Assert.Single(fixture.Transport.Requests);
        Assert.DoesNotContain(fixture.Workspace.Messages, message => message.Content.Contains("snapshot reply", StringComparison.Ordinal));
        Assert.DoesNotContain(await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token), message => message.Role == MessageRole.Assistant);
        Assert.Contains(PullRequestText.Failure("source_scope_denied"), fixture.Workspace.Activity, StringComparison.Ordinal);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task StopCancelsProviderAndOnlyAnExplicitNewSendCanContinue() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestReviewChatFixture.CreateAsync();
        var task = await fixture.OpenAsync();
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Transport.RespondAsync = async token =>
        {
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { cancelled.TrySetResult(); }
            return ReviewChatModelTransport.Reply();
        };
        fixture.Workspace.ComposerText = "manually cancelled review";
        Assert.True(fixture.Workspace.PrimaryActionCommand.CanExecute(null));
        fixture.Workspace.PrimaryActionCommand.Execute(null);
        await fixture.Transport.Started.Task.WaitAsync(TimeSpan.FromSeconds(20), fixture.Token);

        fixture.Workspace.PrimaryActionCommand.Execute(null);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(20), fixture.Token);
        await fixture.WaitUntilAsync(() => !fixture.Workspace.IsRunning && fixture.Workspace.Catalog.SelectedTask?.Status == AgentTaskStatus.Cancelled);

        Assert.Single(fixture.Transport.Requests);
        Assert.Single(await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token));
        fixture.Transport.RespondAsync = null;
        await fixture.SendAsync("explicit continuation");
        Assert.Equal(2, fixture.Transport.Requests.Count);
        Assert.Equal(3, (await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token)).Count);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task RevocationAfterOneVisibleDeltaRejectsTheNextDeltaAndPreservesOnlyAuthorizedOutput() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestReviewChatFixture.CreateAsync();
        var task = await fixture.OpenAsync();
        var binding = (await fixture.Chats.GetAsync(task.Id, fixture.Token))!;
        using var stream = new ReviewChatEventStream();
        fixture.Transport.RespondAsync = _ =>
        {
            var content = new StreamContent(stream);
            content.Headers.ContentType = new("text/event-stream");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        };
        stream.Publish("authorized first delta");
        fixture.Workspace.ComposerText = "manual streamed review";
        Assert.True(fixture.Workspace.PrimaryActionCommand.CanExecute(null));
        fixture.Workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Workspace.Messages.Any(message => message.Content.Contains("authorized first delta", StringComparison.Ordinal)));

        await fixture.Source.Grants.RevokeAsync(binding.Scope.GrantId, fixture.Token);
        stream.Publish("forbidden second delta"); stream.Complete();
        await fixture.WaitUntilAsync(() => !fixture.Workspace.IsRunning && fixture.Workspace.Catalog.SelectedTask?.Status == AgentTaskStatus.Failed);

        Assert.DoesNotContain(fixture.Workspace.Messages, message => message.Content.Contains("forbidden second delta", StringComparison.Ordinal));
        var messages = await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token);
        Assert.Contains(messages, message => message.Role == MessageRole.Assistant && message.Content == "authorized first delta");
        Assert.DoesNotContain(messages, message => message.Content.Contains("forbidden second delta", StringComparison.Ordinal));
        Assert.Single(fixture.Transport.Requests);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("file.read")]
    [InlineData("source.pr_get")]
    public Task UnsolicitedModelToolCallsCannotRunLocalOrSourceTools(string tool) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestReviewChatFixture.CreateAsync();
        var task = await fixture.OpenAsync();
        var sourceRequests = fixture.Source.Transport.RequestCount;
        var frame = JsonSerializer.Serialize(new { type = "response.function_call_arguments.done", name = tool, call_id = "unsolicited", arguments = "{}" });
        fixture.Transport.RespondAsync = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("data: " + frame + "\n\ndata: [DONE]\n\n") });
        fixture.Workspace.ComposerText = "review without tools";

        Assert.True(fixture.Workspace.PrimaryActionCommand.CanExecute(null));
        fixture.Workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => !fixture.Workspace.IsRunning && fixture.Workspace.Catalog.SelectedTask?.Status == AgentTaskStatus.Failed);

        Assert.Single(fixture.Transport.Requests);
        Assert.Equal(sourceRequests, fixture.Source.Transport.RequestCount);
        Assert.Empty(await fixture.Source.Audit.GetRecentAsync(task.Id, 10, fixture.Token));
        Assert.DoesNotContain(await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token), message => message.Role == MessageRole.Tool);
    }, TestContext.Current.CancellationToken);
}
