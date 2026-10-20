using Avalonia.Headless;
using MossAgent.App.Resources;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PullRequestSourceReadRevocationTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData("source.pr_get", false)]
    [InlineData("source.pr_get", true)]
    [InlineData("source.pr_files", false)]
    [InlineData("source.pr_files", true)]
    [InlineData("source.pr_comments", false)]
    [InlineData("source.pr_comments", true)]
    [InlineData("source.pr_checks", false)]
    [InlineData("source.pr_checks", true)]
    public Task AccessChangeDuringSourceHttpDiscardsDataAndPreventsAnotherModelRequest(string tool, bool credential) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestReviewChatFixture.CreateAsync();
        var task = await fixture.OpenAsync(readOnly: true);
        var binding = (await fixture.Chats.GetAsync(task.Id, fixture.Token))!;
        var changed = false;
        fixture.Source.Transport.RespondAsync = async (request, token) =>
        {
            var response = await PullRequestInspectionUiFixture.RespondAsync(request, token);
            var path = request.RequestUri!.AbsolutePath;
            var target = tool == "source.pr_get" ? "/pulls/7" : tool == "source.pr_files" ? "/files"
                : tool == "source.pr_comments" ? "/comments" : "/check-runs";
            if (path.EndsWith(target, StringComparison.Ordinal))
            {
                await ChangeAccessAsync(fixture, binding.Scope, credential, token);
                changed = true;
            }
            return response;
        };
        SourceReadChatScenario.UseTool(fixture, tool, SourceReadChatScenario.Arguments(tool));
        fixture.Workspace.ComposerText = "explicit source read before access change";

        fixture.Workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => !fixture.Workspace.IsRunning && fixture.Workspace.Catalog.SelectedTask?.Status == AgentTaskStatus.Failed);

        Assert.True(changed); Assert.Single(fixture.Transport.Requests);
        var audit = Assert.Single(await fixture.Source.Audit.GetRecentAsync(task.Id, 10, fixture.Token));
        Assert.False(audit.IsSuccess); Assert.NotNull(audit.CompletedAt);
        Assert.Contains(audit.ErrorCode, new[] { "source_scope_denied", "tool_cancelled" });
        Assert.Contains(PullRequestText.Failure("source_scope_denied"), fixture.Workspace.Activity, StringComparison.Ordinal);
        Assert.DoesNotContain(await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token),
            message => message.Role is MessageRole.Assistant or MessageRole.Tool);
        Assert.DoesNotContain(fixture.Workspace.Messages, message => message.Content.Contains("snapshot reply", StringComparison.Ordinal));
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task AccessChangeBeforeSourceChatSendPreservesDraftAndDoesNotRun(bool credential) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestReviewChatFixture.CreateAsync();
        var task = await fixture.OpenAsync(readOnly: true);
        var binding = (await fixture.Chats.GetAsync(task.Id, fixture.Token))!;
        var count = fixture.Source.Transport.RequestCount;
        await ChangeAccessAsync(fixture, binding.Scope, credential, fixture.Token);
        fixture.Workspace.ComposerText = "keep this unsent source query";

        fixture.Workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => !fixture.Workspace.IsRunning && fixture.Workspace.HasSendPreparationError);

        Assert.Equal("keep this unsent source query", fixture.Workspace.ComposerText);
        Assert.Empty(fixture.Transport.Requests); Assert.Equal(count, fixture.Source.Transport.RequestCount);
        Assert.Empty(await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token));
    }, TestContext.Current.CancellationToken);

    private static async Task ChangeAccessAsync(PullRequestReviewChatFixture fixture, SourceRepositoryScope scope,
        bool credential, CancellationToken token)
    {
        if (!credential) { await fixture.Source.Grants.RevokeAsync(scope.GrantId, token); return; }
        var account = (await fixture.Source.Accounts.GetAsync(scope.AccountId, token))!;
        await fixture.Source.Accounts.SaveAsync(new(account.Id, account.Name, account.ApiEndpoint, "changed-synthetic-token",
            account.ProxyId, account.IsEnabled, account.Revision), token);
    }
}
