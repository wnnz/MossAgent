using System.Net;
using Avalonia.Headless;
using MossAgent.App.Resources;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PullRequestLinkFailureTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData("https://github.com/fixture/repo/issues/7?token=synthetic-query", "InvalidPrUrl")]
    [InlineData("https://github.com.attacker.test/fixture/repo/pull/7", "LinkAccountMismatch")]
    [InlineData("https://github.com:8443/fixture/repo/pull/7", "LinkAccountMismatch")]
    [InlineData("https://github.com/fixture/another/pull/7", "LinkRepositoryNotGranted")]
    public Task InvalidLinksRemainEditableWithoutHttpOrNewAuthorization(string url, string resource) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestLinkTestSetup.CreateAsync();
        var model = fixture.Main.Pages.PullRequests!;
        model.Link.ShowCommand.Execute(null); model.Link.Account = Assert.Single(model.Accounts); model.Link.Url = url;
        await model.Link.OpenCommand.ExecuteAsync(null);
        Assert.True(model.Link.IsOpen); Assert.False(model.IsBusy);
        Assert.Equal(PullRequestText.Get(resource), model.Link.Status); Assert.Equal(url, model.Link.Url);
        Assert.DoesNotContain("synthetic-query", model.Link.Status, StringComparison.Ordinal);
        Assert.Equal(0, fixture.Transport.RequestCount); Assert.Single(await fixture.Grants.GetAllAsync(fixture.Token));
        Assert.Null(model.Detail); Assert.False(model.Inspection.IsActive);
        model.Link.Url = "https://github.com/fixture/repo/pull/7";
        Assert.Equal(0, fixture.Transport.RequestCount);
        await model.Link.OpenCommand.ExecuteAsync(null);
        Assert.False(model.Link.IsOpen, model.Link.Status); Assert.Equal(7, model.Detail?.Number);
        Assert.Equal(1, fixture.Transport.RequestCount);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task UnconfiguredRepositoryCanOpenTheDialogButCannotSilentlyGrantAccess() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestLinkTestSetup.CreateAsync(grant: false);
        var model = fixture.Main.Pages.PullRequests!;
        Assert.True(model.Link.ShowCommand.CanExecute(null)); Assert.False(model.LoadCommand.CanExecute(null));
        model.Link.ShowCommand.Execute(null); model.Link.Account = Assert.Single(model.Accounts);
        model.Link.Url = "https://github.com/fixture/repo/pull/7";
        await model.Link.OpenCommand.ExecuteAsync(null);
        Assert.Equal(PullRequestText.Get("LinkRepositoryNotGranted"), model.Link.Status);
        Assert.Empty(await fixture.Grants.GetAllAsync(fixture.Token)); Assert.Equal(0, fixture.Transport.RequestCount);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task MissingAccountKeepsSubmissionDisabledAndOffersTheSettingsRecoveryPath() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await SourceAccountUiFixture.CreateAsync();
        var model = fixture.Main.Pages.PullRequests!;
        fixture.Main.Pages.OpenPullRequestsCommand.Execute(null); await model.RefreshCommand.ExecutionTask!;
        model.Link.ShowCommand.Execute(null); model.Link.Url = "https://github.com/fixture/repo/pull/7";
        Assert.True(model.Link.IsOpen); Assert.Empty(model.Link.Accounts); Assert.Null(model.Link.Account);
        Assert.False(model.Link.OpenCommand.CanExecute(null));
        Assert.Contains("设置", PullRequestText.LinkGrantHint, StringComparison.Ordinal);
        await model.Link.OpenCommand.ExecuteAsync(null);
        Assert.Equal(0, fixture.Transport.RequestCount); Assert.Empty(await fixture.Grants.GetAllAsync(fixture.Token));
        model.Link.CloseCommand.Execute(null); fixture.Main.OpenSettingsCommand.Execute(null);
        Assert.False(model.Link.IsOpen); Assert.False(fixture.Main.Pages.IsPullRequestsOpen);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task AnotherAccountOnTheSameHostCannotBorrowTheFirstAccountsGrant() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestLinkTestSetup.CreateAsync();
        var other = await fixture.Accounts.SaveAsync(new(Guid.NewGuid(), "Other", new Uri("https://api.github.com/"), "synthetic-other-token", null, true), fixture.Token);
        var model = fixture.Main.Pages.PullRequests!; await model.OpenAsync();
        model.Link.ShowCommand.Execute(null); model.Link.Account = model.Accounts.Single(account => account.Id == other.Id);
        model.Link.Url = "https://github.com/fixture/repo/pull/7";
        await model.Link.OpenCommand.ExecuteAsync(null);
        Assert.Equal(PullRequestText.Get("LinkRepositoryNotGranted"), model.Link.Status);
        Assert.Equal(0, fixture.Transport.RequestCount); Assert.Single(await fixture.Grants.GetAllAsync(fixture.Token));
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "AuthFailed")]
    [InlineData(HttpStatusCode.Forbidden, "Forbidden")]
    [InlineData(HttpStatusCode.NotFound, "NotFound")]
    [InlineData(HttpStatusCode.TooManyRequests, "RateLimited")]
    [InlineData(HttpStatusCode.Found, "InvalidResponse")]
    public Task RemoteFailureIsAuditedAndRequiresAnExplicitRetry(HttpStatusCode status, string resource) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestLinkTestSetup.CreateAsync();
        var model = fixture.Main.Pages.PullRequests!;
        fixture.Transport.RespondAsync = (_, _) => Task.FromResult(new HttpResponseMessage(status)
        { Content = new StringContent("synthetic-sensitive-response"), Headers = { Location = new Uri("https://attacker.invalid/") } });
        model.Link.ShowCommand.Execute(null); model.Link.Account = Assert.Single(model.Accounts);
        model.Link.Url = "https://github.com/fixture/repo/pull/7";
        await model.Link.OpenCommand.ExecuteAsync(null);
        Assert.True(model.Link.IsOpen); Assert.Equal(PullRequestText.Get(resource), model.Link.Status);
        Assert.DoesNotContain("synthetic-sensitive-response", model.Link.Status, StringComparison.Ordinal);
        Assert.Null(model.Detail); Assert.Equal(1, fixture.Transport.RequestCount);
        var taskId = await fixture.ReviewSessions.GetOrCreateAsync(model.SelectedRepository!.Scope, fixture.Token);
        Assert.False(Assert.Single(await fixture.Audit.GetRecentAsync(taskId, 10, fixture.Token)).IsSuccess);
        fixture.Transport.RespondAsync = PullRequestInspectionUiFixture.RespondAsync;
        Assert.Equal(1, fixture.Transport.RequestCount);
        await model.Link.OpenCommand.ExecuteAsync(null);
        Assert.False(model.Link.IsOpen, model.Link.Status); Assert.NotNull(model.Detail);
        Assert.Equal(2, fixture.Transport.RequestCount);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RevocationOrAccountEditDuringHttpRejectsTheReturnedContent(bool editAccount) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestLinkTestSetup.CreateAsync();
        var model = fixture.Main.Pages.PullRequests!;
        var scope = model.SelectedRepository!.Scope;
        var taskId = await fixture.ReviewSessions.GetOrCreateAsync(scope, fixture.Token);
        fixture.Transport.RespondAsync = async (_, token) =>
        {
            if (editAccount) await fixture.Accounts.SaveAsync((await fixture.Accounts.GetAsync(scope.AccountId, token))!, token);
            else await fixture.Grants.RevokeAsync(scope.GrantId, token);
            return new(HttpStatusCode.OK) { Content = new StringContent(PullRequestTestData.Item()) };
        };
        model.Link.ShowCommand.Execute(null); model.Link.Account = Assert.Single(model.Accounts);
        model.Link.Url = "https://github.com/fixture/repo/pull/7";
        await model.Link.OpenCommand.ExecuteAsync(null);
        Assert.True(model.Link.IsOpen); Assert.Equal(PullRequestText.Get("ScopeDenied"), model.Link.Status);
        Assert.Null(model.Detail); Assert.Empty(model.Items); Assert.False(model.Inspection.IsActive);
        Assert.False(Assert.Single(await fixture.Audit.GetRecentAsync(taskId, 10, fixture.Token)).IsSuccess);
        Assert.Equal(1, fixture.Transport.DisposedHandlers);
    }, TestContext.Current.CancellationToken);
}
