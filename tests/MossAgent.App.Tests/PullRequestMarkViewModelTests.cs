using System.Net;
using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PullRequestMarkViewModelTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    private static readonly string HeadB = new('b', 40);
    private static readonly string HeadC = new('c', 40);

    [Fact]
    public Task PageLoadFetchesMarksBeforePublishingAndMapsAllThreeStates() => _session.DispatchAsync(async () =>
    {
        var probe = new PullRequestMarksProbe();
        await using var fixture = await SourceAccountUiFixture.CreateAsync(services =>
        {
            services.RemoveAll<IPullRequestMarks>();
            services.AddSingleton<IPullRequestMarks>(probe);
        });
        var account = await fixture.Accounts.SaveAsync(new(Guid.NewGuid(), "PR connection",
            new Uri("https://api.github.com/"), "synthetic-pr-credential", null, true), fixture.Token);
        await fixture.Grants.AddAsync(new(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo", false), fixture.Token);
        var model = fixture.Main.Pages.PullRequests!;
        var order = new List<string>();
        probe.GetHandler = (accountId, owner, repository, numbers, _) =>
        {
            order.Add("marks");
            return Task.FromResult<IReadOnlyList<PullRequestMark>>(
            [
                new(accountId, owner, repository, 1, HeadB, DateTimeOffset.UtcNow),
                new(accountId, owner, repository, 2, HeadC, DateTimeOffset.UtcNow),
            ]);
        };
        fixture.Transport.RespondAsync = (request, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent($"[{PullRequestTestData.Item(1)},{PullRequestTestData.Item(2)},{PullRequestTestData.Item(3)}]") });
        await model.OpenAsync();
        model.Items.CollectionChanged += (_, _) => order.Add("items");
        await model.LoadCommand.ExecuteAsync(null);
        Assert.Equal("marks", order[0]);
        Assert.All(order.Skip(1), step => Assert.Equal("items", step));
        Assert.Equal(3, model.Items.Count);
        Assert.Equal(PullRequestViewedState.Viewed, model.Items[0].ViewedState);
        Assert.Equal(PullRequestViewedState.UpdatedSinceViewed, model.Items[1].ViewedState);
        Assert.Equal(PullRequestViewedState.Unviewed, model.Items[2].ViewedState);
        Assert.Equal(PullRequestText.MarkViewed, model.Items[0].MarkLabel);
        Assert.Equal(PullRequestText.MarkUpdated, model.Items[1].MarkLabel);
        Assert.Equal(PullRequestText.MarkUnviewed, model.Items[2].MarkLabel);
        Assert.Equal($"get:{account.Id.ToString("N")[..8]}:fixture/repo:1,2,3", Assert.Single(probe.Calls));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task DetailOpenMarksViewedAtCurrentHeadAndRefreshesTheBadge() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await SourceAccountUiFixture.CreateAsync();
        var account = await fixture.Accounts.SaveAsync(new(Guid.NewGuid(), "PR connection",
            new Uri("https://api.github.com/"), "synthetic-pr-credential", null, true), fixture.Token);
        await fixture.Grants.AddAsync(new(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo", false), fixture.Token);
        var marks = fixture.Get<IPullRequestMarks>();
        await marks.MarkViewedAsync(account.Id, "fixture", "repo", 7, HeadC, fixture.Token);
        fixture.Transport.RespondAsync = PullRequestInspectionUiFixture.RespondAsync;
        var model = fixture.Main.Pages.PullRequests!;
        await model.OpenAsync();
        await model.LoadCommand.ExecuteAsync(null);
        Assert.Equal(PullRequestViewedState.UpdatedSinceViewed, Assert.Single(model.Items).ViewedState);
        model.SelectedItem = model.Items[0];
        await model.DetailCommand.ExecuteAsync(null);
        Assert.NotNull(model.Detail);
        var stored = Assert.Single(await marks.GetAsync(account.Id, "fixture", "repo", [7], fixture.Token));
        Assert.Equal(HeadB, stored.HeadRevision);
        Assert.Equal(PullRequestViewedState.Viewed, model.Items[0].ViewedState);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task LinkedOpenMarksViewedAndPublishesTheItemAsViewed() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestLinkTestSetup.CreateAsync();
        var model = fixture.Main.Pages.PullRequests!;
        var marks = fixture.Get<IPullRequestMarks>();
        var account = Assert.Single(model.Accounts);
        model.Link.ShowCommand.Execute(null);
        model.Link.Account = account;
        model.Link.Url = "https://github.com/fixture/repo/pull/7";
        await model.Link.OpenCommand.ExecuteAsync(null);
        Assert.False(model.Link.IsOpen, model.Link.Status);
        var stored = Assert.Single(await marks.GetAsync(account.Id, "fixture", "repo", [7], fixture.Token));
        Assert.Equal(HeadB, stored.HeadRevision);
        Assert.Equal(PullRequestViewedState.Viewed, Assert.Single(model.Items).ViewedState);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task MarkStoreFailureLeavesListAndDetailIntact() => _session.DispatchAsync(async () =>
    {
        var probe = new PullRequestMarksProbe();
        await using var fixture = await SourceAccountUiFixture.CreateAsync(services =>
        {
            services.RemoveAll<IPullRequestMarks>();
            services.AddSingleton<IPullRequestMarks>(probe);
        });
        var account = await fixture.Accounts.SaveAsync(new(Guid.NewGuid(), "PR connection",
            new Uri("https://api.github.com/"), "synthetic-pr-credential", null, true), fixture.Token);
        await fixture.Grants.AddAsync(new(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo", false), fixture.Token);
        probe.MarkHandler = (_, _, _, _, _, _) => throw new SourceReviewException("invalid_arguments");
        fixture.Transport.RespondAsync = PullRequestInspectionUiFixture.RespondAsync;
        var model = fixture.Main.Pages.PullRequests!;
        await model.OpenAsync();
        await model.LoadCommand.ExecuteAsync(null);
        Assert.Equal(PullRequestViewedState.Unviewed, Assert.Single(model.Items).ViewedState);
        model.SelectedItem = model.Items[0];
        await model.DetailCommand.ExecuteAsync(null);
        Assert.NotNull(model.Detail);
        Assert.Equal(PullRequestText.Get("Loaded"), model.Status);
        Assert.Equal(PullRequestViewedState.Unviewed, model.Items[0].ViewedState);
        Assert.Single(probe.Calls, call => call.StartsWith("mark:", StringComparison.Ordinal));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task MarkReadFailureFailsThePageLoadInsteadOfPublishingWrongMarks() => _session.DispatchAsync(async () =>
    {
        var probe = new PullRequestMarksProbe();
        await using var fixture = await SourceAccountUiFixture.CreateAsync(services =>
        {
            services.RemoveAll<IPullRequestMarks>();
            services.AddSingleton<IPullRequestMarks>(probe);
        });
        var account = await fixture.Accounts.SaveAsync(new(Guid.NewGuid(), "PR connection",
            new Uri("https://api.github.com/"), "synthetic-pr-credential", null, true), fixture.Token);
        await fixture.Grants.AddAsync(new(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo", false), fixture.Token);
        probe.GetHandler = (_, _, _, _, _) => throw new SourceReviewException("invalid_arguments");
        fixture.Transport.RespondAsync = PullRequestInspectionUiFixture.RespondAsync;
        var model = fixture.Main.Pages.PullRequests!;
        await model.OpenAsync();
        await model.LoadCommand.ExecuteAsync(null);
        Assert.Equal(PullRequestText.Get("Failed"), model.Status);
        Assert.Empty(model.Items);
        Assert.Single(probe.Calls, call => call.StartsWith("get:", StringComparison.Ordinal));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task MarksStayScopedToTheirAccountAcrossGrantsOfTheSameRepository() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await SourceAccountUiFixture.CreateAsync();
        var first = await fixture.Accounts.SaveAsync(new(Guid.NewGuid(), "PR A",
            new Uri("https://api.github.com/"), "synthetic-pr-credential", null, true), fixture.Token);
        var second = await fixture.Accounts.SaveAsync(new(Guid.NewGuid(), "PR B",
            new Uri("https://api.github.com/"), "synthetic-pr-credential", null, true), fixture.Token);
        await fixture.Grants.AddAsync(new(Guid.NewGuid(), first.Id, first.Revision, "fixture", "repo", false), fixture.Token);
        await fixture.Grants.AddAsync(new(Guid.NewGuid(), second.Id, second.Revision, "fixture", "repo", false), fixture.Token);
        var marks = fixture.Get<IPullRequestMarks>();
        await marks.MarkViewedAsync(first.Id, "fixture", "repo", 7, HeadB, fixture.Token);
        fixture.Transport.RespondAsync = PullRequestInspectionUiFixture.RespondAsync;
        var model = fixture.Main.Pages.PullRequests!;
        await model.OpenAsync();
        model.SelectedRepository = Assert.Single(model.Repositories, item => item.Scope.AccountId == second.Id);
        await model.LoadCommand.ExecuteAsync(null);
        Assert.Equal(PullRequestViewedState.Unviewed, Assert.Single(model.Items).ViewedState);
        model.SelectedRepository = Assert.Single(model.Repositories, item => item.Scope.AccountId == first.Id);
        await model.LoadCommand.ExecuteAsync(null);
        Assert.Equal(PullRequestViewedState.Viewed, Assert.Single(model.Items).ViewedState);
    }, TestContext.Current.CancellationToken);
}
