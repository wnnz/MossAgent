using System.Net;
using MossAgent.Domain;
using MossAgent.Infrastructure.SourceControl;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class GitHubPullRequestClientTests
{
    private static SourceAccount Account => SourceAccountValidationTests.Create().WithRevision(1);
    private static SourceRepositoryScope Scope(SourceAccount account) => new(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo", false);

    [Fact]
    public async Task FixedEndpointPaginationAndDetailNeverFollowServerProvidedLinks()
    {
        var account = Account;
        var scope = Scope(account);
        var probe = new SourceAccountHttpProbe();
        probe.RespondAsync = (request, _) =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal(account.AccessToken, request.Headers.Authorization.Parameter);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.StartsWith(new Uri(account.ApiEndpoint, "repos/fixture/repo/pulls").AbsoluteUri, request.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(request.RequestUri.Query.Length > 0 ? $"[{PullRequestTestData.Item()}]" : PullRequestTestData.Item()) };
            response.Headers.TryAddWithoutValidation("Link", "<https://attacker.invalid/?token=secret>; rel=next");
            return Task.FromResult(response);
        };
        var client = new GitHubPullRequestClient(probe);
        var list = await client.ListAsync(account, scope, 2, TestContext.Current.CancellationToken);
        Assert.Equal(2, list.Page);
        Assert.False(list.HasNextPage);
        Assert.Empty(Assert.Single(list.Items).Body);
        Assert.Equal("Untrusted PR description", (await client.GetAsync(account, scope, 7, TestContext.Current.CancellationToken)).Body);
        Assert.Equal(2, probe.RequestCount);
        Assert.Equal(2, probe.DisposedHandlers);
    }

    [Theory]
    [InlineData(301, "source_redirect_denied")]
    [InlineData(401, "source_auth_failed")]
    [InlineData(403, "source_forbidden")]
    [InlineData(404, "source_not_found")]
    [InlineData(429, "source_rate_limited")]
    [InlineData(500, "source_unavailable")]
    public async Task StatusErrorsDoNotLeakBodiesCredentialsOrRetry(int status, string code)
    {
        var account = Account;
        var probe = new SourceAccountHttpProbe
        {
            RespondAsync = (_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
            { Content = new StringContent(account.AccessToken) })
        };
        var exception = await Assert.ThrowsAsync<SourceReviewException>(() => new GitHubPullRequestClient(probe)
            .ListAsync(account, Scope(account), 1, TestContext.Current.CancellationToken));
        Assert.Equal(code, exception.ErrorCode);
        Assert.DoesNotContain(account.AccessToken, exception.ToString(), StringComparison.Ordinal);
        Assert.Null(exception.InnerException);
        Assert.Equal(1, probe.RequestCount);
        Assert.Equal(1, probe.DisposedHandlers);
    }

    [Theory]
    [InlineData("wrong-repository")]
    [InlineData("wrong-number")]
    [InlineData("malformed")]
    [InlineData("oversized")]
    [InlineData("credential")]
    [InlineData("revision")]
    public async Task UntrustedOrUnboundedDetailIsRejected(string kind)
    {
        var account = Account;
        var body = kind switch
        {
            "wrong-repository" => PullRequestTestData.Item(repository: "other/repo"),
            "wrong-number" => PullRequestTestData.Item(number: 8),
            "oversized" => new string('x', 2 * 1024 * 1024 + 1),
            "credential" => PullRequestTestData.Item(body: account.AccessToken),
            "revision" => PullRequestTestData.Item().Replace(new string('b', 40), "invalid", StringComparison.Ordinal),
            _ => "not json"
        };
        var probe = new SourceAccountHttpProbe { RespondAsync = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }) };
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => new GitHubPullRequestClient(probe)
            .GetAsync(account, Scope(account), 7, TestContext.Current.CancellationToken));
        Assert.Equal("source_invalid_response", failure.ErrorCode);
        Assert.Equal(1, probe.DisposedHandlers);
    }

    [Fact]
    public async Task CancellationWaitsForTransportCleanup()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = new SourceAccountHttpProbe { RespondAsync = async (_, token) =>
        { entered.TrySetResult(); await Task.Delay(Timeout.Infinite, token); return new HttpResponseMessage(HttpStatusCode.OK); } };
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var account = Account;
        var work = new GitHubPullRequestClient(probe).GetAsync(account, Scope(account), 7, cancel.Token);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work);
        Assert.Equal(1, probe.DisposedHandlers);
    }
}
