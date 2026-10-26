using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Threading;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

internal sealed class PullRequestInspectionUiFixture(SourceAccountUiFixture source) : IAsyncDisposable
{
    public SourceAccountUiFixture Source { get; } = source;
    public PullRequestsViewModel Model => Source.Main.Pages.PullRequests!;
    public CancellationToken Token => Source.Token;

    public static async Task<PullRequestInspectionUiFixture> CreateAsync(Action<IServiceCollection>? configure = null)
    {
        var source = await SourceAccountUiFixture.CreateAsync(configure);
        try
        {
            await source.Accounts.SaveAsync(new(Guid.NewGuid(), "PR connection", new Uri("https://api.github.com/"), "synthetic-pr-credential", null, true), source.Token);
            var result = new PullRequestInspectionUiFixture(source);
            source.Transport.RespondAsync = RespondAsync;
            await result.Model.OpenAsync();
            result.Model.CreateCommand.Execute(null); result.Model.Owner = "fixture"; result.Model.Repository = "repo";
            await result.Model.SaveGrantCommand.ExecuteAsync(null);
            await result.Model.LoadCommand.ExecuteAsync(null);
            result.Model.SelectedItem = result.Model.Items[0];
            await result.Model.DetailCommand.ExecuteAsync(null);
            Assert.NotNull(result.Model.Detail);
            return result;
        }
        catch { await source.DisposeAsync(); throw; }
    }

    public static Task<HttpResponseMessage> RespondAsync(HttpRequestMessage request, CancellationToken token)
    {
        Assert.False(Dispatcher.UIThread.CheckAccess());
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("api.github.com", request.RequestUri!.Host);
        var path = request.RequestUri.AbsolutePath;
        var body = path.EndsWith("/files", StringComparison.Ordinal) ? $"[{PullRequestTestData.File()}]"
            : path.EndsWith("/comments", StringComparison.Ordinal) ? $"[{PullRequestTestData.Comment()}]"
            : path.EndsWith("/reviews", StringComparison.Ordinal) ? $"[{PullRequestTestData.Review()}]"
            : path.EndsWith("/events", StringComparison.Ordinal) ? $"[{PullRequestTestData.Event()}]"
            : path.EndsWith("/check-runs", StringComparison.Ordinal) ? PullRequestTestData.Checks()
            : path.EndsWith("/status", StringComparison.Ordinal) ? PullRequestTestData.Checks(false)
            : request.RequestUri.Query.Length > 0 ? $"[{PullRequestTestData.Item()}]" : PullRequestTestData.Item();
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
    }

    public ValueTask DisposeAsync() => Source.DisposeAsync();
}
