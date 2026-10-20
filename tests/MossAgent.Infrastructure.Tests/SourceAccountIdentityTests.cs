using System.Net;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Infrastructure.SourceControl;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SourceAccountIdentityTests
{
    [Fact]
    public async Task RefreshActuallyAuthenticatesAtPinnedEndpointEachTime()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var accounts = new SqliteSourceAccountRepository(fixture.Connections);
        var account = await accounts.SaveAsync(SourceAccountValidationTests.Create(), fixture.Token);
        var probe = new SourceAccountHttpProbe();
        probe.RespondAsync = (request, _) =>
        {
            Assert.Equal("https://enterprise.example.test/api/v3/user", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal(account.AccessToken, request.Headers.Authorization?.Parameter);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.False(request.Headers.Contains("Cookie"));
            return Task.FromResult(Response(HttpStatusCode.OK, $"{{\"id\":42,\"login\":\"user-{probe.RequestCount}\"}}"));
        };
        var connections = new SourceAccountConnections(accounts, new GitHubIdentityClient(probe));
        Assert.Equal("user-1", (await connections.RefreshIdentityAsync(account.Id, account.Revision, fixture.Token)).Login);
        Assert.Equal("user-2", (await connections.RefreshIdentityAsync(account.Id, account.Revision, fixture.Token)).Login);
        Assert.Equal(2, probe.DisposedHandlers);
    }

    [Theory]
    [InlineData(401, SourceConnectionFailure.InvalidCredential)]
    [InlineData(403, SourceConnectionFailure.Forbidden)]
    [InlineData(429, SourceConnectionFailure.RateLimited)]
    [InlineData(302, SourceConnectionFailure.RedirectDenied)]
    [InlineData(500, SourceConnectionFailure.ServiceUnavailable)]
    public async Task ErrorsNeverExposeBodyLocationOrCredential(int status, SourceConnectionFailure expected)
    {
        var account = SourceAccountValidationTests.Create();
        var probe = new SourceAccountHttpProbe
        {
            RespondAsync = (_, _) =>
            {
                var response = Response((HttpStatusCode)status, account.AccessToken);
                response.Headers.Location = new Uri("https://other.example.test/?token=" + account.AccessToken);
                return Task.FromResult(response);
            }
        };
        var exception = await Assert.ThrowsAsync<SourceConnectionException>(() =>
            new GitHubIdentityClient(probe).GetIdentityAsync(account, TestContext.Current.CancellationToken));
        Assert.Equal(expected, exception.Failure);
        Assert.DoesNotContain(account.AccessToken, exception.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, probe.RequestCount);
        Assert.Equal(1, probe.DisposedHandlers);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"id\":\"42\",\"login\":\"user\"}")]
    [InlineData("{\"id\":0,\"login\":\"user\"}")]
    [InlineData("{\"id\":42,\"login\":\"<script>\"}")]
    [InlineData("{\"id\":42,\"login\":\"synthetic-source-credential\"}")]
    public async Task InvalidIdentityIsRejectedWithBoundedDiagnostic(string body)
    {
        var probe = new SourceAccountHttpProbe { RespondAsync = (_, _) => Task.FromResult(Response(HttpStatusCode.OK, body)) };
        var exception = await Assert.ThrowsAsync<SourceConnectionException>(() => new GitHubIdentityClient(probe)
            .GetIdentityAsync(SourceAccountValidationTests.Create(), TestContext.Current.CancellationToken));
        Assert.Equal(SourceConnectionFailure.InvalidResponse, exception.Failure);
        Assert.DoesNotContain(body, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownLengthOversizedBodyIsStoppedAndDisposed()
    {
        var stream = new SourceIdentityTestStream(128 * 1024);
        var probe = new SourceAccountHttpProbe
        {
            RespondAsync = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) })
        };
        var exception = await Assert.ThrowsAsync<SourceConnectionException>(() => new GitHubIdentityClient(probe)
            .GetIdentityAsync(SourceAccountValidationTests.Create(), TestContext.Current.CancellationToken));
        Assert.Equal(SourceConnectionFailure.InvalidResponse, exception.Failure);
        Assert.True(stream.Disposed);
        Assert.Equal(1, probe.DisposedHandlers);
    }

    [Fact]
    public async Task CancellationWaitsForBodyAndClientCleanup()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var stream = new SourceIdentityTestStream(0, block: true);
        var probe = new SourceAccountHttpProbe
        {
            RespondAsync = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) })
        };
        var task = new GitHubIdentityClient(probe).GetIdentityAsync(SourceAccountValidationTests.Create(), cancellation.Token);
        await stream.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.True(stream.Disposed);
        Assert.Equal(1, probe.DisposedHandlers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AccountChangedDuringRequestCannotPublishOldIdentity(bool delete)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var accounts = new SqliteSourceAccountRepository(fixture.Connections);
        var saved = await accounts.SaveAsync(SourceAccountValidationTests.Create(), fixture.Token);
        var probe = new SourceAccountHttpProbe
        {
            RespondAsync = async (_, token) =>
            {
                if (delete) await accounts.DeleteAsync(saved.Id, saved.Revision, token);
                else await accounts.SaveAsync(new(saved.Id, saved.Name, saved.ApiEndpoint, saved.AccessToken,
                    saved.ProxyId, false, saved.Revision), token);
                return Response(HttpStatusCode.OK, "{\"id\":42,\"login\":\"old-user\"}");
            }
        };
        var exception = await Assert.ThrowsAsync<SourceConnectionException>(() =>
            new SourceAccountConnections(accounts, new GitHubIdentityClient(probe))
                .RefreshIdentityAsync(saved.Id, saved.Revision, fixture.Token));
        Assert.Equal(SourceConnectionFailure.AccountChanged, exception.Failure);
        Assert.Equal(1, probe.RequestCount);
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body) };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleOrDisabledAccountIsRejectedBeforeAuthentication(bool disabled)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var accounts = new SqliteSourceAccountRepository(fixture.Connections);
        var saved = await accounts.SaveAsync(SourceAccountValidationTests.Create(), fixture.Token);
        await accounts.SaveAsync(new(saved.Id, saved.Name, saved.ApiEndpoint, saved.AccessToken,
            saved.ProxyId, !disabled, saved.Revision), fixture.Token);
        var probe = new SourceAccountHttpProbe();
        var connections = new SourceAccountConnections(accounts, new GitHubIdentityClient(probe));
        var failure = await Assert.ThrowsAsync<SourceConnectionException>(() =>
            connections.RefreshIdentityAsync(saved.Id, saved.Revision, fixture.Token));
        Assert.Equal(disabled ? SourceConnectionFailure.AccountUnavailable : SourceConnectionFailure.AccountChanged, failure.Failure);
        Assert.Equal(0, probe.RequestCount);
    }
}
