using System.Text.Json;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.SourceControl;

public sealed class GitHubPullRequestBranchClient(ISourceAccountHttpClientFactory clients) : IPullRequestBranchClient
{
    private readonly GitHubSourceTransport _transport = new(clients);

    // One bounded page of up to 100 branches. GitHubInspectionJson.Items caps arrays
    // at 20, so this enumerates the root array directly — the same shape the inbox
    // parser uses for its own bounded lists — with the cap applied on the length.
    public async Task<IReadOnlyList<PullRequestBranch>> ListAsync(SourceAccount account, SourceRepositoryScope scope,
        CancellationToken cancellationToken)
    {
        Validate(account, scope);
        var response = await _transport.GetAsync(account,
            $"repos/{scope.Owner}/{scope.Repository}/branches?per_page=100", cancellationToken);
        try { return Parse(response, account.AccessToken); }
        catch (SourceReviewException) { throw; }
        catch (Exception) { throw new SourceReviewException("source_invalid_response"); }
    }

    private static PullRequestBranch[] Parse(byte[] body, string token)
    {
        using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 24 });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() > 100) throw GitHubInspectionJson.Invalid();
        var branches = root.EnumerateArray().Select(item => ParseItem(item, token)).ToArray();
        if (branches.Select(branch => branch.Name).Distinct(StringComparer.Ordinal).Count() != branches.Length)
            throw GitHubInspectionJson.Invalid();
        return branches;
    }

    private static PullRequestBranch ParseItem(JsonElement item, string token)
    {
        if (item.ValueKind != JsonValueKind.Object) throw GitHubInspectionJson.Invalid();
        var name = GitHubInspectionJson.Text(item, "name", 255, token);
        if (!PullRequestBranch.IsValidName(name)) throw GitHubInspectionJson.Invalid();
        var sha = GitHubInspectionJson.Text(item.GetProperty("commit"), "sha", 64, token);
        return PullRequestRevision.IsCommit(sha)
            ? new PullRequestBranch(name, sha) : throw GitHubInspectionJson.Invalid();
    }

    private static void Validate(SourceAccount account, SourceRepositoryScope scope)
    {
        if (!account.IsEnabled || account.Id != scope.AccountId || account.Revision != scope.AccountRevision)
            throw new SourceReviewException("source_scope_denied");
        if (SourceRepositoryScope.ValidateSegment(scope.Owner, true) != scope.Owner
            || SourceRepositoryScope.ValidateSegment(scope.Repository) != scope.Repository)
            throw new SourceReviewException("invalid_arguments");
    }
}
