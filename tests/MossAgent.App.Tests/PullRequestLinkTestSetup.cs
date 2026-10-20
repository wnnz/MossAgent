using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal static class PullRequestLinkTestSetup
{
    public static async Task<SourceAccountUiFixture> CreateAsync(string endpoint = "https://api.github.com/", bool grant = true)
    {
        var source = await SourceAccountUiFixture.CreateAsync();
        try
        {
            var account = await source.Accounts.SaveAsync(new(Guid.NewGuid(), "Link source", new Uri(endpoint), "synthetic-link-credential", null, true), source.Token);
            if (grant) await source.Grants.AddAsync(new(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo"), source.Token);
            source.Transport.RespondAsync = PullRequestInspectionUiFixture.RespondAsync;
            source.Main.Pages.OpenPullRequestsCommand.Execute(null);
            var model = source.Main.Pages.PullRequests!;
            await model.RefreshCommand.ExecutionTask!;
            return source;
        }
        catch { await source.DisposeAsync(); throw; }
    }
}
