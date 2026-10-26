using System.Text.Json;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.SourceControl;

public sealed class GitHubPullRequestWriteClient(ISourceAccountHttpClientFactory clients) : IPullRequestWriteClient
{
    private const int MaximumBodyLength = 4096;
    private readonly GitHubSourceTransport _transport = new(clients);

    public async Task<long> CommentAsync(SourceAccount account, SourceRepositoryScope scope, PullRequestRevision revision,
        string body, CancellationToken cancellationToken)
    {
        revision.Validate();
        Validate(account, scope);
        if (string.IsNullOrEmpty(body) || body.Length > MaximumBodyLength) throw new SourceReviewException("invalid_arguments");
        var payload = JsonSerializer.SerializeToUtf8Bytes(new { body });
        var response = await _transport.PostAsync(account,
            $"repos/{scope.Owner}/{scope.Repository}/issues/{revision.Number}/comments", payload, cancellationToken);
        try
        {
            using var document = JsonDocument.Parse(response, new JsonDocumentOptions { MaxDepth = 24 });
            var item = document.RootElement;
            if (item.ValueKind != JsonValueKind.Object) throw GitHubInspectionJson.Invalid();
            var expected = new Uri(account.ApiEndpoint, $"repos/{scope.Owner}/{scope.Repository}/issues/{revision.Number}");
            GitHubInspectionJson.CheckOwner(item, "issue_url", expected, account.AccessToken);
            return GitHubInspectionJson.Id(item);
        }
        catch (SourceReviewException) { throw; }
        catch (Exception) { throw new SourceReviewException("source_invalid_response"); }
    }

    public async Task<long?> FindCommentAsync(SourceAccount account, SourceRepositoryScope scope, int number,
        string body, CancellationToken cancellationToken)
    {
        Validate(account, scope);
        if (number <= 0 || string.IsNullOrEmpty(body) || body.Length > MaximumBodyLength)
            throw new SourceReviewException("invalid_arguments");
        var response = await _transport.GetAsync(account,
            $"repos/{scope.Owner}/{scope.Repository}/issues/{number}/comments?per_page=5&sort=created&direction=desc",
            cancellationToken);
        try
        {
            using var document = JsonDocument.Parse(response, new JsonDocumentOptions { MaxDepth = 24 });
            var expected = new Uri(account.ApiEndpoint, $"repos/{scope.Owner}/{scope.Repository}/issues/{number}");
            foreach (var item in GitHubInspectionJson.Items(document.RootElement))
            {
                GitHubInspectionJson.CheckOwner(item, "issue_url", expected, account.AccessToken);
                if (GitHubInspectionJson.OptionalText(item, "body", 16384, account.AccessToken, allowEmpty: true) == body)
                    return GitHubInspectionJson.Id(item);
            }
            return null;
        }
        catch (SourceReviewException) { throw; }
        catch (Exception) { throw new SourceReviewException("source_invalid_response"); }
    }

    private static void Validate(SourceAccount account, SourceRepositoryScope scope)
    {
        if (!account.IsEnabled || !scope.CanWrite || account.Id != scope.AccountId || account.Revision != scope.AccountRevision)
            throw new SourceReviewException("source_scope_denied");
        if (SourceRepositoryScope.ValidateSegment(scope.Owner, true) != scope.Owner
            || SourceRepositoryScope.ValidateSegment(scope.Repository) != scope.Repository)
            throw new SourceReviewException("invalid_arguments");
    }
}
