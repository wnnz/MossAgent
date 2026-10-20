using System.Net;
using System.Text.Json;
using Avalonia.Headless;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PullRequestLinkChatWorkflowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task LinkToAnotherPrOpensItsOwnReadOnlyChatAndQueriesOnlyThatPr() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestReviewChatFixture.CreateAsync();
        var previous = await fixture.OpenAsync(readOnly: true);
        fixture.Workspace.ComposerText = "preserved previous PR draft";
        await fixture.Workspace.Drafts.FlushAsync();
        await fixture.ReturnToPullRequestAsync();
        var count = fixture.Source.Transport.RequestCount;
        fixture.Source.Transport.RespondAsync = (request, _) =>
        {
            Assert.Equal("https://api.github.com/repos/fixture/repo/pulls/42", request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(PullRequestTestData.Item(42, title: "Another linked PR")) });
        };
        fixture.Page.Link.ShowCommand.Execute(null); fixture.Page.Link.Account = Assert.Single(fixture.Page.Accounts);
        fixture.Page.Link.Url = "https://github.com/fixture/repo/pull/42?token=synthetic-query#comment";
        await fixture.Page.Link.OpenCommand.ExecuteAsync(null);
        Assert.False(fixture.Page.Link.IsOpen, fixture.Page.Link.Status);
        Assert.Equal(count + 1, fixture.Source.Transport.RequestCount); Assert.Empty(fixture.Transport.Requests);
        var task = await fixture.OpenAsync(readOnly: true);
        var binding = (await fixture.Chats.GetAsync(task.Id, fixture.Token))!;
        Assert.Equal(42, binding.Revision.Number); Assert.NotEqual(previous.Id, task.Id);
        Assert.Equal(SourceReviewChatMode.ReadOnlySource, binding.Mode);
        Assert.Null(task.ProjectId); Assert.Null(task.WorktreePath); Assert.Equal(ApprovalPolicy.ReadOnly, task.ApprovalPolicy);
        Assert.DoesNotContain("synthetic-query", binding.Context, StringComparison.Ordinal);
        Assert.Equal(count + 1, fixture.Source.Transport.RequestCount); Assert.Empty(fixture.Transport.Requests);
        SourceReadChatScenario.UseTool(fixture, "source.pr_get", SourceReadChatScenario.Arguments("source.pr_get", number: 42));
        await SourceReadChatScenario.SendAsync(fixture, "Query the linked PR only.");
        Assert.Equal(count + 2, fixture.Source.Transport.RequestCount); Assert.Equal(2, fixture.Transport.Requests.Count);
        using var result = SourceReadChatScenario.ToolOutput(fixture.Transport.Requests.Last());
        Assert.True(result.RootElement.GetProperty("IsSuccess").GetBoolean());
        using var content = JsonDocument.Parse(result.RootElement.GetProperty("Content").GetString()!);
        Assert.Equal(42, content.RootElement.GetProperty("Number").GetInt32());
        Assert.True(Assert.Single(await fixture.Source.Audit.GetRecentAsync(task.Id, 10, fixture.Token)).IsSuccess);
        Assert.Equal("preserved previous PR draft", (await fixture.Drafts.GetAsync(new(null, previous.Id), fixture.Token))!.Text);
    }, TestContext.Current.CancellationToken);
}
